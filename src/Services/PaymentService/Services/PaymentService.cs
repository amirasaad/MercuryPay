using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Models;
using MassTransit;
using MercuryPay.PaymentService.Infrastructure;
using MercuryPay.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePayment(PaymentRequest request);
    Task<PaymentResponse?> GetPayment(Guid id);
}

public class PaymentService(PaymentDbContext context, IPublishEndpoint publishEndpoint, ILogger<PaymentService> logger) : IPaymentService
{
    private readonly PaymentDbContext _context = context;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<PaymentService> _logger = logger;

    public async Task<PaymentResponse> CreatePayment(PaymentRequest request)
    {
        _logger.LogInformation("Creating payment from {FromUserId} to {ToUserId} for {Amount} {Currency}", 
            request.FromUserId, request.ToUserId, request.Amount, request.Currency);

        if (request.Amount <= 0)
        {
            _logger.LogWarning("Invalid payment amount: {Amount}", request.Amount);
            throw new ArgumentException("Amount must be positive");
        }

        var paymentId = Guid.NewGuid();
        var payment = new Payment(paymentId, request.FromUserId, request.ToUserId, request.Amount, request.Currency, "Pending", request.ReferenceId);

        await _context.Payments.AddAsync(payment);
        
        await _publishEndpoint.Publish(new PaymentCreated(
            paymentId,
            request.FromUserId,
            request.ToUserId,
            request.Amount,
            request.Currency,
            DateTimeOffset.UtcNow,
            request.ReferenceId
        ));

        var isHighValue = request.Amount >= 10000m;
        var isSuspiciousUser = request.FromUserId.StartsWith("suspicious", StringComparison.OrdinalIgnoreCase);
        var approved = !(isHighValue || isSuspiciousUser);
        var reason = approved
            ? "Transaction passed all risk checks - low risk"
            : (isHighValue
                ? $"Transaction amount (${request.Amount:F2}) exceeds high-value threshold ($10000.00)"
                : $"User '{request.FromUserId}' matches suspicious user pattern");

        await _publishEndpoint.Publish(new FraudEvaluated(
            paymentId,
            approved,
            approved ? 10 : 90,
            reason,
            DateTimeOffset.UtcNow,
            request.ReferenceId
        ));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment {PaymentId} created successfully", payment.Id);

        return new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.FromUserId,
            payment.ToUserId,
            payment.ReferenceId
        );
    }

    public async Task<PaymentResponse?> GetPayment(Guid id)
    {
        var payment = await _context.Payments.FindAsync(id);
        
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", id);
            return null;
        }

        return new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.FromUserId,
            payment.ToUserId,
            payment.ReferenceId
        );
    }
}
