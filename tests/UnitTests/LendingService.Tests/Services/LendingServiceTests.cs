using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Services;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;
using Xunit;

namespace MercuryPay.LendingService.Tests.Services;

public class LendingServiceTests
{
    private readonly LendingDbContext _context;
    private readonly Mock<ILogger<MercuryPay.LendingService.Services.LendingService>> _mockLogger;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly MercuryPay.LendingService.Services.LendingService _service;

    public LendingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LendingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new LendingDbContext(options);
        _mockLogger = new Mock<ILogger<MercuryPay.LendingService.Services.LendingService>>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _service = new MercuryPay.LendingService.Services.LendingService(_context, _mockLogger.Object, _mockPublishEndpoint.Object);
    }

    [Fact]
    public async Task CreateLoan_ShouldSucceed_WhenAmountExceedsLimit()
    {
        // Arrange
        var userId = "user-123";
        var amount = 100001m; // Exceeds 100,000 limit
        var currency = "USD";
        var termMonths = 12;

        // Act
        var loan = await _service.CreateLoan(userId, amount, currency, termMonths);

        // Assert
        Assert.NotNull(loan);
        Assert.Equal(amount, loan.Amount);
        Assert.Equal("Processing", loan.Status);
    }

    [Fact]
    public async Task CreateLoan_ShouldSucceed_WhenAmountIsWithinLimit()
    {
        // Arrange
        var userId = "user-123";
        var amount = 50000m; // Within limit
        var currency = "USD";
        var termMonths = 12;

        // Act
        var loan = await _service.CreateLoan(userId, amount, currency, termMonths);

        // Assert
        Assert.NotNull(loan);
        Assert.Equal(amount, loan.Amount);
    }
}
