using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.BuildingBlocks.Events;
using MassTransit;

namespace MercuryPay.LendingService.Services;

public interface ILendingService
{
    Task<Loan> CreateLoan(string userId, decimal amount, string currency, int termMonths);
    Task<Loan?> GetLoan(Guid id);
    Task<List<Loan>> GetLoansByUser(string userId);
    Task<bool> RetryDisbursement(Guid loanId);
    Task<bool> RepayLoan(Guid loanId, decimal amount);
}

public class LendingService(LendingDbContext context, ILogger<LendingService> logger, IPublishEndpoint publishEndpoint) : ILendingService
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LendingService> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private const decimal DefaultAnnualInterestRate = 0.05m; // 5% Fixed for MVP
    private const decimal MaxLoanAmount = 100000m;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _repaymentGates = new();

    public async Task<Loan> CreateLoan(string userId, decimal amount, string currency, int termMonths)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("Invalid loan amount: {Amount}", amount);
            throw new ArgumentException("Amount must be positive");
        }

        if (amount > MaxLoanAmount)
        {
            _logger.LogWarning("Loan amount {Amount} exceeds maximum limit of {MaxAmount}.", amount, MaxLoanAmount);
            throw new ArgumentException("Amount exceeds maximum allowed");
        }

        if (termMonths <= 0 || termMonths > 120)
        {
            _logger.LogWarning("Invalid loan term: {Term}", termMonths);
            throw new ArgumentException("Term must be between 1 and 120 months");
        }

        var loan = new Loan(Guid.NewGuid(), userId, amount, currency, "Processing", DateTime.UtcNow, termMonths, DefaultAnnualInterestRate);
        
        // Generate schedule immediately
        loan.GenerateRepaymentSchedule();
        
        _context.Loans.Add(loan);
        
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Loan {LoanId} created for user {UserId}. Status: Processing, Term: {Term} months", loan.Id, userId, termMonths);
        }

        // Publish LoanCreated event (Async Processing)
        await _publishEndpoint.Publish(new LoanCreated(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));
        
        await _context.SaveChangesAsync();
        
        return loan;
    }

    public async Task<Loan?> GetLoan(Guid id)
    {
        return await QueryLoansWithRepaymentSchedule()
            .FirstOrDefaultAsync(loan => loan.Id == id);
    }

    public async Task<List<Loan>> GetLoansByUser(string userId)
    {
        return await QueryLoansWithRepaymentSchedule()
            .Where(loan => loan.UserId == userId)
            .ToListAsync();
    }

    public async Task<bool> RetryDisbursement(Guid loanId)
    {
        var loan = await QueryLoansWithRepaymentSchedule()
            .FirstOrDefaultAsync(existingLoan => existingLoan.Id == loanId);
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

        _logger.LogInformation("Retrying disbursement for Loan {LoanId}", loanId);

        // Publish LoanApproved event again
        await _publishEndpoint.Publish(new LoanApproved(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RepayLoan(Guid loanId, decimal amount)
    {
        var gate = _repaymentGates.GetOrAdd(loanId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            // Fetch loan first (compatible with InMemory provider)
            var loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
            
            if (loan == null)
            {
                _logger.LogWarning("Loan {LoanId} not found for repayment", loanId);
                return false;
            }

            if (loan.Status != "Approved" && loan.Status != "Active")
            {
                _logger.LogWarning("Loan {LoanId} status is {Status}, cannot repay", loanId, loan.Status);
                return false;
            }

            // Update status
            loan.MarkAsRepaymentProcessing();

            _logger.LogInformation("Initiating repayment for Loan {LoanId} Amount {Amount}", loanId, amount);

            // Publish LoanRepaymentRequested event
            await _publishEndpoint.Publish(new LoanRepaymentRequested(
                loan.Id,
                loan.UserId,
                amount, // Use the requested amount
                loan.Currency,
                DateTimeOffset.UtcNow
            ));

            await _context.SaveChangesAsync();

            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private IQueryable<Loan> QueryLoansWithRepaymentSchedule()
    {
        return _context.Loans
            .Include(loan => loan.RepaymentSchedule)
            .ThenInclude(schedule => schedule!.Installments);
    }
}
