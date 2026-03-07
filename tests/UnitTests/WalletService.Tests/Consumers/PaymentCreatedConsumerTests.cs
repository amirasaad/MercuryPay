using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Consumers;
using MercuryPay.WalletService.Domain;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MercuryPay.WalletService.Tests.Consumers;

public class PaymentCreatedConsumerTests : IDisposable
{
    private readonly WalletDbContext _context;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly PaymentCreatedConsumer _consumer;

    public PaymentCreatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new WalletDbContext(options);
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _consumer = new PaymentCreatedConsumer(_context, _mockPublishEndpoint.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task Consume_CreditsAndDebitsWallets_WhenWalletsExist()
    {
        // Arrange
        var fromUserId = "user_1";
        var toUserId = "user_2";
        var amount = 100m;
        var currency = "USD";

        // Seed wallets
        var fromWallet = new Wallet(Guid.NewGuid(), fromUserId, currency);
        fromWallet.Credit(500m, "INIT", "Initial Balance");
        var toWallet = new Wallet(Guid.NewGuid(), toUserId, currency);

        _context.Wallets.AddRange(fromWallet, toWallet);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var paymentId = Guid.NewGuid();
        var message = new PaymentCreated(paymentId, fromUserId, toUserId, amount, currency, DateTimeOffset.UtcNow);
        
        var contextMock = new Mock<ConsumeContext<PaymentCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        var updatedFromWallet = await _context.Wallets.FindAsync(fromWallet.Id);
        var updatedToWallet = await _context.Wallets.FindAsync(toWallet.Id);

        Assert.Equal(400m, updatedFromWallet!.Balance);
        Assert.Equal(100m, updatedToWallet!.Balance);
        
        // Verify ledger entries
        Assert.Contains(updatedFromWallet.Ledger, l => l.Amount == -amount && l.TransactionId == paymentId.ToString());
        Assert.Contains(updatedToWallet.Ledger, l => l.Amount == amount && l.TransactionId == paymentId.ToString());
    }

    [Fact]
    public async Task Consume_DoesNothing_WhenFromWalletDoesNotExist()
    {
        // Arrange
        var fromUserId = "user_missing";
        var toUserId = "user_2";
        var amount = 100m;
        
        var toWallet = new Wallet(Guid.NewGuid(), toUserId, "USD");
        _context.Wallets.Add(toWallet);
        await _context.SaveChangesAsync();

        var message = new PaymentCreated(Guid.NewGuid(), fromUserId, toUserId, amount, "USD", DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<PaymentCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        var updatedToWallet = await _context.Wallets.FindAsync(toWallet.Id);
        Assert.Equal(0m, updatedToWallet!.Balance); // Should not have changed
    }

    [Fact]
    public async Task Consume_IsIdempotent_WhenMessageIsProcessedTwice()
    {
        // Arrange
        var fromWallet = new Wallet(Guid.NewGuid(), "user_1", "USD");
        fromWallet.Credit(500m, "INIT", "Initial Balance");
        var toWallet = new Wallet(Guid.NewGuid(), "user_2", "USD");
        _context.Wallets.AddRange(fromWallet, toWallet);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var message = new PaymentCreated(Guid.NewGuid(), "user_1", "user_2", 100m, "USD", DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<PaymentCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act - First process
        await _consumer.Consume(contextMock.Object);
        
        // Clear change tracker to simulate fresh context load for second process
        _context.ChangeTracker.Clear();
        
        // Act - Second process (duplicate)
        await _consumer.Consume(contextMock.Object);

        // Assert
        var updatedFromWallet = await _context.Wallets.Include(w => w.Ledger).FirstOrDefaultAsync(w => w.Id == fromWallet.Id);
        var updatedToWallet = await _context.Wallets.Include(w => w.Ledger).FirstOrDefaultAsync(w => w.Id == toWallet.Id);

        Assert.Equal(400m, updatedFromWallet!.Balance); // Should be deducted only once
        Assert.Equal(100m, updatedToWallet!.Balance);   // Should be credited only once
        
        // Check ledger entries count
        Assert.Equal(2, updatedFromWallet.Ledger.Count); // INIT + 1 Debit
        Assert.Equal(1, updatedToWallet.Ledger.Count);   // 1 Credit
    }
}
