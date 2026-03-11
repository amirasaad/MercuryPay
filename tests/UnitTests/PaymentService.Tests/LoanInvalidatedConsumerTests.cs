using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Consumers;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MercuryPay.PaymentService.Domain;

namespace MercuryPay.PaymentService.Tests;

public class LoanInvalidatedConsumerTests
{
    private readonly PaymentDbContext _dbContext;
    private readonly Mock<ILogger<LoanInvalidatedConsumer>> _loggerMock;
    private readonly LoanInvalidatedConsumer _consumer;

    public LoanInvalidatedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PaymentDbContext(options);
        _loggerMock = new Mock<ILogger<LoanInvalidatedConsumer>>();
        _consumer = new LoanInvalidatedConsumer(_dbContext, _loggerMock.Object);
    }

    [Fact]
    public async Task Consume_ShouldCancelPendingPayments_WhenLoanIsInvalidated()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var pendingPayment = new Payment(Guid.NewGuid(), "LendingService", "user_123", 1000m, "USD", "Pending", loanId);
        var approvedPayment = new Payment(Guid.NewGuid(), "LendingService", "user_123", 1000m, "USD", "Approved", loanId);
        
        await _dbContext.Payments.AddRangeAsync(pendingPayment, approvedPayment);
        await _dbContext.SaveChangesAsync();

        var message = new LoanInvalidated(loanId, "Loan amount exceeds limit", DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<LoanInvalidated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await _consumer.Consume(contextMock.Object);

        // Assert
        var updatedPendingPayment = await _dbContext.Payments.FindAsync(pendingPayment.Id);
        var updatedApprovedPayment = await _dbContext.Payments.FindAsync(approvedPayment.Id);

        Assert.NotNull(updatedPendingPayment);
        Assert.Equal("Rejected", updatedPendingPayment.Status);
        
        Assert.NotNull(updatedApprovedPayment);
        Assert.Equal("Approved", updatedApprovedPayment.Status); // Should not change approved payments
    }
}
