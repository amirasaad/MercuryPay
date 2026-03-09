using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Models;
using MercuryPay.PaymentService.Services;

namespace MercuryPay.PaymentService.Consumers;

public class LoanApprovedConsumer(IPaymentService paymentService, ILogger<LoanApprovedConsumer> logger) : IConsumer<LoanApproved>
{
    private readonly IPaymentService _paymentService = paymentService;
    private readonly ILogger<LoanApprovedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanApproved> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing loan disbursement for Loan {LoanId}, User {UserId}, Amount {Amount} {Currency}", 
            message.LoanId, message.UserId, message.Amount, message.Currency);

        try
        {
            // Create a payment to disburse the loan
            // We use a system account "LendingService" as the source
            var paymentRequest = new PaymentRequest(
                message.Amount, 
                message.Currency,
                "LendingService", 
                message.UserId,
                message.LoanId
            );

            var payment = await _paymentService.CreatePayment(paymentRequest);

            _logger.LogInformation("Loan {LoanId} disbursed via Payment {PaymentId}", message.LoanId, payment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disburse loan {LoanId}", message.LoanId);
            // Throwing exception triggers retry/fault policies
            throw;
        }
    }
}
