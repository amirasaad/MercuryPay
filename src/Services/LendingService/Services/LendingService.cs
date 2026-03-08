using Microsoft.EntityFrameworkCore;
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
    Task<bool> RetryDisbursement(Guid loanId);
    Task<bool> RepayLoan(Guid loanId);
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

        var loan = new Loan(Guid.NewGuid(), userId, amount, currency, "Processing", DateTime.UtcNow);
        
        _context.Loans.Add(loan);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Loan {LoanId} created for user {UserId}. Status: Processing", loan.Id, userId);

        // Publish LoanCreated event (Async Processing)
        await _publishEndpoint.Publish(new LoanCreated(
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
        return await _context.Loans.Where(l => l.UserId == userId).ToListAsync();
    }

    public async Task<bool> RetryDisbursement(Guid loanId)
    {
        var loan = await _context.Loans.FindAsync(loanId);
        if (loan == null)
        {
            _logger.LogWarning("Loan {LoanId} not found for retry", loanId);
            return false;
        }

        if (loan.Status != "DisbursementFailed")
        {
            _logger.LogWarning("Loan {LoanId} status is {Status}, cannot retry disbursement", loanId, loan.Status);
            return false;
        }

        loan.RetryDisbursement();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Retrying disbursement for Loan {LoanId}", loanId);

        // Publish LoanApproved event again
        await _publishEndpoint.Publish(new LoanApproved(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));

        return true;
    }

    public async Task<bool> RepayLoan(Guid loanId)
    {
        // Atomically update status from Approved to RepaymentProcessing to prevent race conditions
        var rowsAffected = await _context.Loans
            .Where(l => l.Id == loanId && l.Status == "Approved")
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Status, "RepaymentProcessing"));

        if (rowsAffected == 0)
        {
            var loanCheck = await _context.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loanId);
            if (loanCheck == null)
            {
                _logger.LogWarning("Loan {LoanId} not found for repayment", loanId);
            }
            else
            {
                _logger.LogWarning("Loan {LoanId} status is {Status}, cannot repay", loanId, loanCheck.Status);
            }
            return false;
        }

        // Fetch fresh entity to get details for event
        var loan = await _context.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loanId);
        
        if (loan == null) return false; // Should not happen

        _logger.LogInformation("Initiating repayment for Loan {LoanId}", loanId);

        // Publish LoanRepaymentRequested event
        await _publishEndpoint.Publish(new LoanRepaymentRequested(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));

        return true;
    }
}
