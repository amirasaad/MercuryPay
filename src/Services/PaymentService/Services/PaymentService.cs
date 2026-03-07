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

public class PaymentService(PaymentDbContext context, IPublishEndpoint publishEndpoint) : IPaymentService
{
    private readonly PaymentDbContext _context = context;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<PaymentResponse> CreatePayment(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be positive");
        }

        var paymentId = NewId.NextGuid();
        var payment = new Payment(paymentId, request.FromUserId, request.ToUserId, request.Amount, request.Currency, "Pending");

        _context.Payments.Add(payment);
        
        // Publish event (will be captured by Outbox)
        await _publishEndpoint.Publish(new PaymentCreated(
            paymentId,
            request.FromUserId,
            request.ToUserId,
            request.Amount,
            request.Currency,
            DateTimeOffset.UtcNow
        ));

        // Save changes (commits both Payment entity and Outbox message atomically)
        await _context.SaveChangesAsync();

        return new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.FromUserId,
            payment.ToUserId
        );
    }

    public async Task<PaymentResponse?> GetPayment(Guid id)
    {
        var payment = await _context.Payments.FindAsync(id);
        
        if (payment == null) return null;

        return new PaymentResponse(
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.FromUserId,
            payment.ToUserId
        );
    }
}
