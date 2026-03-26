using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Consumers;
using MercuryPay.WalletService.Domain;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MercuryPay.WalletService.Tests.Consumers;

public class FraudEvaluatedConsumerTests : IDisposable
{
    private readonly WalletDbContext _db;
    private readonly Mock<IPublishEndpoint> _publish;
    private readonly Mock<ILogger<FraudEvaluatedConsumer>> _logger;

    public FraudEvaluatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WalletDbContext(options);
        _publish = new Mock<IPublishEndpoint>();
        _logger = new Mock<ILogger<FraudEvaluatedConsumer>>();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Consume_WhenRejected_ReversesFundsByPaymentId()
    {
        var from = new Wallet(Guid.NewGuid(), "lender", "USD");
        from.Credit(1_000m, "INIT", "seed");
        var to = new Wallet(Guid.NewGuid(), "borrower", "USD");
        _db.Wallets.AddRange(from, to);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var paymentId = Guid.NewGuid();
        var created = new PaymentCreated(paymentId, "lender", "borrower", 250m, "USD", DateTimeOffset.UtcNow);
        var createdCtx = new Mock<ConsumeContext<PaymentCreated>>();
        createdCtx.Setup(x => x.Message).Returns(created);
        var paymentCreatedConsumer = new PaymentCreatedConsumer(_db, _publish.Object, Mock.Of<ILogger<PaymentCreatedConsumer>>());
        await paymentCreatedConsumer.Consume(createdCtx.Object);

        _db.ChangeTracker.Clear();
        var consumer = new FraudEvaluatedConsumer(_db, _publish.Object, _logger.Object);
        var msg = new FraudEvaluated(paymentId, false, 95, "fraud", DateTimeOffset.UtcNow, null);
        var ctx = new Mock<ConsumeContext<FraudEvaluated>>();
        ctx.Setup(x => x.Message).Returns(msg);
        await consumer.Consume(ctx.Object);

        var fromUpdated = await _db.Wallets.Include(w => w.Ledger).FirstAsync(w => w.UserId == "lender");
        var toUpdated = await _db.Wallets.Include(w => w.Ledger).FirstAsync(w => w.UserId == "borrower");

        Assert.Equal(1_000m, fromUpdated.Balance);
        Assert.Equal(0m, toUpdated.Balance);
        Assert.Contains(fromUpdated.Ledger, l => l.TransactionId == $"{paymentId}:reversal" && l.Amount == 250m);
        Assert.Contains(toUpdated.Ledger, l => l.TransactionId == $"{paymentId}:reversal" && l.Amount == -250m);
    }

    [Fact]
    public async Task Consume_WhenRejectedAndFundsSpent_EmitsReviewAndDoesNotChangeBalances()
    {
        var from = new Wallet(Guid.NewGuid(), "lender", "USD");
        from.Credit(500m, "INIT", "seed");
        var to = new Wallet(Guid.NewGuid(), "borrower", "USD");
        _db.Wallets.AddRange(from, to);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var paymentId = Guid.NewGuid();
        var created = new PaymentCreated(paymentId, "lender", "borrower", 300m, "USD", DateTimeOffset.UtcNow);
        var createdCtx = new Mock<ConsumeContext<PaymentCreated>>();
        createdCtx.Setup(x => x.Message).Returns(created);
        var paymentCreatedConsumer = new PaymentCreatedConsumer(_db, _publish.Object, Mock.Of<ILogger<PaymentCreatedConsumer>>());
        await paymentCreatedConsumer.Consume(createdCtx.Object);

        _db.ChangeTracker.Clear();
        var borrower = await _db.Wallets.Include(w => w.Ledger).FirstAsync(w => w.UserId == "borrower");
        borrower.Debit(300m, "SPEND", "spend");
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var consumer = new FraudEvaluatedConsumer(_db, _publish.Object, _logger.Object);
        var msg = new FraudEvaluated(paymentId, false, 99, "fraud", DateTimeOffset.UtcNow, null);
        var ctx = new Mock<ConsumeContext<FraudEvaluated>>();
        ctx.Setup(x => x.Message).Returns(msg);
        await consumer.Consume(ctx.Object);

        var fromUpdated = await _db.Wallets.FirstAsync(w => w.UserId == "lender");
        var toUpdated = await _db.Wallets.FirstAsync(w => w.UserId == "borrower");
        Assert.Equal(200m, fromUpdated.Balance);
        Assert.Equal(0m, toUpdated.Balance);
        _publish.Verify(p => p.Publish(It.IsAny<PaymentRequiresReview>(), default), Times.Once);
    }
}
