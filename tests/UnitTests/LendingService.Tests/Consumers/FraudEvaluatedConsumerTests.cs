using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Consumers;
using MercuryPay.LendingService.Domain;
using MercuryPay.BuildingBlocks.Events;

namespace MercuryPay.LendingService.Tests.Consumers;

public class FraudEvaluatedConsumerTests
{
    [Fact]
    public async Task FraudEvaluated_WhenFlagged_CancelsInstallments_AndPublishesEvent()
    {
        var options = new DbContextOptionsBuilder<LendingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new LendingDbContext(options);

        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
        loan.GenerateRepaymentSchedule();
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<FraudEvaluatedConsumer>>();
        var published = new List<object>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((msg, _) => published.Add(msg))
            .Returns(Task.CompletedTask);
        publishEndpoint
            .Setup(p => p.Publish(It.IsAny<LoanFraudDetected>(), It.IsAny<CancellationToken>()))
            .Callback<LoanFraudDetected, CancellationToken>((msg, _) => published.Add(msg))
            .Returns(Task.CompletedTask);

        var consumer = new FraudEvaluatedConsumer(db, logger.Object, publishEndpoint.Object);

        var message = new FraudEvaluated(Guid.NewGuid(), false, 95, "High risk", DateTimeOffset.UtcNow, loan.Id);
        var ctx = Mock.Of<ConsumeContext<FraudEvaluated>>(c => c.Message == message);

        await consumer.Consume(ctx);

        var updated = await db.Loans.Include(l => l.RepaymentSchedule)!.ThenInclude(s => s!.Installments)
            .FirstAsync(l => l.Id == loan.Id);
        Assert.Equal("FraudDetected", updated.Status);
        Assert.All(updated.RepaymentSchedule!.Installments, i =>
            Assert.True(i.Status == "Cancelled" || i.Status == "Paid"));
        Assert.Contains(published, e => e is LoanFraudDetected lf && lf.LoanId == loan.Id);
    }
}
