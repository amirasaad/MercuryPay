using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Domain;
using MercuryPay.PaymentService.Infrastructure;

namespace MercuryPay.PaymentService.Consumers;

public class LoanApprovedConsumer(PaymentDbContext context, ILogger<LoanApprovedConsumer> logger) : IConsumer<LoanApproved>
{
    private readonly PaymentDbContext _context = context;
    private readonly ILogger<LoanApprovedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanApproved> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing loan disbursement for Loan {LoanId}, User {UserId}, Amount {Amount} {Currency}", 
            message.LoanId, message.UserId, message.Amount, message.Currency);

        // Check if disbursement already exists (idempotency)
        // In a real system, we'd use a unique index or specific Disbursement entity.
        // For now, we'll check if a payment exists with the same ID (if we used LoanId as PaymentId)
        // or just log it. 
        // Let's create a new payment ID for the disbursement.
        
        var paymentId = NewId.NextGuid();
        var disbursement = new Payment(
            paymentId,
            "LendingService", // From
            message.UserId,   // To
            message.Amount,
            message.Currency,
            "Completed" // Assume immediate disbursement for now
        );

        _context.Payments.Add(disbursement);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Loan {LoanId} disbursed via Payment {PaymentId}", message.LoanId, paymentId);
    }
}
