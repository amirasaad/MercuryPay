using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Consumers;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MercuryPay.PaymentService.Tests;

public class LoanApprovedConsumerTests
{
    private readonly PaymentDbContext _dbContext;
    private readonly Mock<ILogger<LoanApprovedConsumer>> _loggerMock;
    private readonly LoanApprovedConsumer _consumer;

    public LoanApprovedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PaymentDbContext(options);
        _loggerMock = new Mock<ILogger<LoanApprovedConsumer>>();
        _consumer = new LoanApprovedConsumer(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_ShouldCreatePayment_WhenLoanIsApproved()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var userId = "user_123";
        var amount = 1000m;
        var currency = "USD";
        var message = new LoanApproved(loanId, userId, amount, currency, DateTimeOffset.UtcNow);

        var contextMock = new Mock<ConsumeContext<LoanApproved>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.ToUserId == userId && p.Amount == amount);
        Assert.NotNull(payment);
        Assert.Equal("LendingService", payment.FromUserId);
        Assert.Equal("Completed", payment.Status);
    }
}
