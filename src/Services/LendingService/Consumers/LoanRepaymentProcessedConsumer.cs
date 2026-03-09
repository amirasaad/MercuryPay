using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class LoanRepaymentProcessedConsumer(LendingDbContext context, ILogger<LoanRepaymentProcessedConsumer> logger) : IConsumer<LoanRepaymentProcessed>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LoanRepaymentProcessedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanRepaymentProcessed> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing repayment result for Loan {LoanId}, Amount: {Amount}, Success: {Success}", 
            message.LoanId, message.Amount, message.Success);

        // Explicitly include RepaymentSchedule to ensure it's loaded
        // Note: For owned types, Include might not be enough if nested owned collection is not auto-loaded
        // But for OwnsOne -> OwnsMany, usually it is loaded.
        // Let's rely on default loading behavior first, or try explicit query if needed.
        // But since RepaymentSchedule is a record, we can't easily navigate via ThenInclude if it's not exposed as navigation property in a way EF recognizes easily with lambdas if types mismatch?
        // Actually, RepaymentSchedule is the property name.
        var loan = await _context.Loans
            .Include(l => l.RepaymentSchedule)
            .ThenInclude(rs => rs.Installments)
            .FirstOrDefaultAsync(l => l.Id == message.LoanId);

        if (loan == null)
        {
            _logger.LogWarning("Loan {LoanId} not found during repayment processing", message.LoanId);
            return;
        }

        if (message.Success)
        {
            _logger.LogInformation("Loan {LoanId} found. RepaymentSchedule present: {HasSchedule}. Installments count: {Count}", 
                message.LoanId, 
                loan.RepaymentSchedule != null, 
                loan.RepaymentSchedule?.Installments?.Count ?? 0);

            loan.ProcessRepayment(message.Amount);
            _logger.LogInformation("Loan {LoanId} processed repayment of {Amount}", message.LoanId, message.Amount);
            
            // Force update to ensure EF Core detects changes in owned collection
            _context.Update(loan);
        }
        else
        {
            loan.MarkAsRepaymentFailed();
            _logger.LogWarning("Loan {LoanId} repayment failed: {Reason}", message.LoanId, message.FailureReason);
        }

        await _context.SaveChangesAsync();
    }
}
