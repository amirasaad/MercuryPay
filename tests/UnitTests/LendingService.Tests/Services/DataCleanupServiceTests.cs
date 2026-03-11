using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Services;
using MercuryPay.LendingService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MercuryPay.LendingService.Tests.Services;

public class DataCleanupServiceTests
{
    private readonly Mock<ILogger<DataCleanupService>> _loggerMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly LendingDbContext _dbContext;

    public DataCleanupServiceTests()
    {
        _loggerMock = new Mock<ILogger<DataCleanupService>>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();

        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<LendingDbContext>(options => 
            options.UseInMemoryDatabase(dbName)); 
        services.AddScoped(_ => _publishEndpointMock.Object);
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<LendingDbContext>();
    }

    [Fact]
    public async Task TriggerCleanupAsync_ShouldInvalidateLoansExceedingLimit()
    {
        var excessiveLoan = new Loan(Guid.NewGuid(), "user1", 150000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
        
        var validLoan = new Loan(Guid.NewGuid(), "user2", 50000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);

        await _dbContext.Loans.AddRangeAsync(excessiveLoan, validLoan);
        await _dbContext.SaveChangesAsync();

        _dbContext.Entry(excessiveLoan).State = EntityState.Detached;
        _dbContext.Entry(validLoan).State = EntityState.Detached;

        var service = new DataCleanupService(_serviceProvider, _loggerMock.Object);

        await service.TriggerCleanupAsync(CancellationToken.None);

        var updatedExcessiveLoan = await _dbContext.Loans.FindAsync(excessiveLoan.Id);
        var updatedValidLoan = await _dbContext.Loans.FindAsync(validLoan.Id);

        Assert.Equal("Invalid", updatedExcessiveLoan!.Status);
        Assert.Equal("Approved", updatedValidLoan!.Status);

        _publishEndpointMock.Verify(x => x.Publish(
            It.Is<LoanInvalidated>(e => e.LoanId == excessiveLoan.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        _publishEndpointMock.Verify(x => x.Publish(
            It.Is<LoanInvalidated>(e => e.LoanId == validLoan.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TriggerCleanupAsync_ShouldNotInvalidateAlreadyInvalidLoans()
    {
        var alreadyInvalidLoan = new Loan(Guid.NewGuid(), "user1", 150000m, "USD", "Invalid", DateTime.UtcNow, 12, 0.05m);
        
        await _dbContext.Loans.AddAsync(alreadyInvalidLoan);
        await _dbContext.SaveChangesAsync();

        var service = new DataCleanupService(_serviceProvider, _loggerMock.Object);

        await service.TriggerCleanupAsync(CancellationToken.None);

        _publishEndpointMock.Verify(x => x.Publish(
            It.IsAny<LoanInvalidated>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
