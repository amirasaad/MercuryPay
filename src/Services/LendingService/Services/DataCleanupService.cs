using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Services;

public class DataCleanupService(IServiceProvider serviceProvider, ILogger<DataCleanupService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<DataCleanupService> _logger = logger;
    private const decimal MaxLoanAmount = 100000m;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Data Cleanup Service...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LendingDbContext>();

            // Ensure database is ready
            await context.Database.EnsureCreatedAsync(stoppingToken);

            // Find loans exceeding the limit that are not already marked as Invalid or Repaid/RepaymentFailed
            // We specifically target "Approved" or "Processing" or "Active" loans with excessive amounts
            var invalidLoans = await context.Loans
                .Where(l => l.Amount > MaxLoanAmount && 
                            l.Status != "Invalid" && 
                            l.Status != "Repaid" && 
                            l.Status != "RepaymentFailed")
                .ToListAsync(stoppingToken);

            if (invalidLoans.Count > 0)
            {
                _logger.LogWarning("Found {Count} loans exceeding the maximum limit of {MaxAmount}. Marking them as Invalid.", invalidLoans.Count, MaxLoanAmount);

                foreach (var loan in invalidLoans)
                {
                    _logger.LogInformation("Invalidating Loan {LoanId} with amount {Amount}", loan.Id, loan.Amount);
                    loan.MarkAsInvalid();
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Successfully invalidated {Count} loans.", invalidLoans.Count);
            }
            else
            {
                _logger.LogInformation("No invalid loans found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during data cleanup.");
        }
    }
}
