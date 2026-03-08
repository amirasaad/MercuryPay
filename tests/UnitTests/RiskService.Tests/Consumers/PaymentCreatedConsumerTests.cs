using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.RiskService.Consumers;
using MercuryPay.RiskService.Domain;
using MercuryPay.RiskService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MercuryPay.RiskService.Tests.Consumers;

public class PaymentCreatedConsumerTests
{
    private readonly Mock<ILogger<PaymentCreatedConsumer>> _loggerMock;
    private readonly RiskDbContext _dbContext;

    public PaymentCreatedConsumerTests()
    {
        _loggerMock = new Mock<ILogger<PaymentCreatedConsumer>>();
        
        var options = new DbContextOptionsBuilder<RiskDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _dbContext = new RiskDbContext(options);
    }

    [Fact]
    public async Task Consume_ShouldEvaluateRiskAndSaveToDatabase()
    {
        // Arrange
        var consumer = new PaymentCreatedConsumer(_loggerMock.Object, _dbContext);
        var contextMock = new Mock<ConsumeContext<PaymentCreated>>();
        
        var paymentId = Guid.NewGuid();
        var message = new PaymentCreated(
            paymentId,
            "user-123",
            "user-456",
            100.00m,
            "USD",
            DateTimeOffset.UtcNow
        );
        
        contextMock.SetupGet(x => x.Message).Returns(message);
        
        // Mock Publish
        contextMock.Setup(x => x.Publish(It.IsAny<FraudEvaluated>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var assessment = await _dbContext.RiskAssessments.FirstOrDefaultAsync(a => a.PaymentId == paymentId);
        Assert.NotNull(assessment);
        Assert.Equal(paymentId, assessment.PaymentId);
        Assert.True(assessment.IsApproved); // Default low risk for 100.00
        
        // Verify event was published with correct data
        contextMock.Verify(x => x.Publish(
            It.Is<FraudEvaluated>(e => 
                e.PaymentId == paymentId && 
                e.IsApproved == assessment.IsApproved &&
                e.RiskScore == assessment.RiskScore
            ),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_HighValue_ShouldRejectAndSave()
    {
        // Arrange
        var consumer = new PaymentCreatedConsumer(_loggerMock.Object, _dbContext);
        var contextMock = new Mock<ConsumeContext<PaymentCreated>>();
        
        var paymentId = Guid.NewGuid();
        var message = new PaymentCreated(
            paymentId,
            "user-vip",
            "merchant-xyz",
            15000.00m, // > 10000 -> High Risk
            "USD",
            DateTimeOffset.UtcNow
        );
        
        contextMock.SetupGet(x => x.Message).Returns(message);
        contextMock.Setup(x => x.Publish(It.IsAny<FraudEvaluated>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var assessment = await _dbContext.RiskAssessments.FirstOrDefaultAsync(a => a.PaymentId == paymentId);
        Assert.NotNull(assessment);
        Assert.False(assessment.IsApproved);
        Assert.Equal("High Value Transaction (> 10,000)", assessment.Reason);
        
        // Verify event
        contextMock.Verify(x => x.Publish(
            It.Is<FraudEvaluated>(e => !e.IsApproved),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
