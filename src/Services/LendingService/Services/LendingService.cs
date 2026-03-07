using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.BuildingBlocks.Events;
using MassTransit;

namespace MercuryPay.LendingService.Services;

public interface ILendingService
{
    Task<Loan> CreateLoan(string userId, decimal amount, string currency);
    Task<Loan?> GetLoan(Guid id);
    Task<List<Loan>> GetLoansByUser(string userId);
}

public class LendingService(LendingDbContext context, ILogger<LendingService> logger, IPublishEndpoint publishEndpoint) : ILendingService
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LendingService> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<Loan> CreateLoan(string userId, decimal amount, string currency)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("Invalid loan amount: {Amount}", amount);
            throw new ArgumentException("Amount must be positive");
        }

        var loan = new Loan(Guid.NewGuid(), userId, amount, currency, "Approved", DateTime.UtcNow); // Auto-approve for now
        
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Loan {LoanId} created for user {UserId}", loan.Id, userId);

        // Publish LoanApproved event
        await _publishEndpoint.Publish(new LoanApproved(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));
        
        return loan;
    }

    public async Task<Loan?> GetLoan(Guid id)
    {
        return await _context.Loans.FindAsync(id);
    }

    public async Task<List<Loan>> GetLoansByUser(string userId)
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            System.Linq.Queryable.Where(_context.Loans, l => l.UserId == userId));
    }
}
