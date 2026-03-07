using System.Collections.Concurrent;
using MercuryPay.PaymentService.Models;
using MassTransit;
using MercuryPay.BuildingBlocks.Events;

namespace MercuryPay.PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePayment(PaymentRequest request);
    Task<PaymentResponse?> GetPayment(Guid id);
}

public class PaymentService(IPublishEndpoint publishEndpoint) : IPaymentService
{
    private static readonly ConcurrentDictionary<Guid, PaymentResponse> _payments = new();
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task<PaymentResponse> CreatePayment(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be positive");
        }

        var response = new PaymentResponse(
            Guid.NewGuid(),
            "Pending",
            request.Amount,
            request.Currency,
            request.FromUserId,
            request.ToUserId
        );

        _payments[response.Id] = response;

        await _publishEndpoint.Publish(new PaymentCreated(
            response.Id,
            response.FromUserId,
            response.ToUserId,
            response.Amount,
            response.Currency,
            DateTimeOffset.UtcNow
        ));

        return response;
    }

    public Task<PaymentResponse?> GetPayment(Guid id)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult(payment);
    }
}
