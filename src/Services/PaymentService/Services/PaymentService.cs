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

        // Idempotency: if a ReferenceId is supplied, return the existing payment rather than
        // creating a duplicate. This protects against client retries after a 5xx response that
        // may have arrived after the Payment row was already committed.
        if (request.ReferenceId.HasValue)
        {
            var existing = await _context.Payments
                .FirstOrDefaultAsync(p => p.ReferenceId == request.ReferenceId);
            if (existing != null)
            {
                _logger.LogInformation("Payment {PaymentId} already exists for ReferenceId {ReferenceId} — returning existing record",
                    existing.Id, request.ReferenceId);
                return new PaymentResponse(
                    existing.Id,
                    existing.Status,
                    existing.Amount,
                    existing.Currency,
                    existing.FromUserId,
                    existing.ToUserId,
                    existing.ReferenceId,
                    existing.RejectionReason
                );
            }
        }

        var paymentId = Guid.NewGuid();
        var payment = new Payment(paymentId, request.FromUserId, request.ToUserId, request.Amount, request.Currency, "Pending", request.ReferenceId);

        await _context.Payments.AddAsync(payment);

        // Commit the Payment record first so that the wallet consumer can always find
        // a matching payment row if it queries back to PaymentService.
        await _context.SaveChangesAsync();

        // Publish events directly to the broker after the DB commit.
        // This is intentionally outside a transactional outbox: the Payment row is
        // already durable at this point, so the only risk is a publish failure that
        // causes the HTTP handler to return 5xx (triggering a client retry).
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

        _logger.LogInformation("Payment {PaymentId} created successfully", payment.Id);

        return new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.FromUserId,
            payment.ToUserId,
            payment.ReferenceId,
            payment.RejectionReason
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
            payment.ReferenceId,
            payment.RejectionReason
        );
    }
}
