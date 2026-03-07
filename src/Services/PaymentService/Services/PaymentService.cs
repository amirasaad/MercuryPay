using System.Collections.Concurrent;
using MercuryPay.PaymentService.Models;

namespace MercuryPay.PaymentService.Services;

public interface IPaymentService
{
    PaymentResponse CreatePayment(PaymentRequest request);
    PaymentResponse? GetPayment(Guid id);
}

public class PaymentService : IPaymentService
{
    private static readonly ConcurrentDictionary<Guid, PaymentResponse> _payments = new();

    public PaymentResponse CreatePayment(PaymentRequest request)
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
        return response;
    }

    public PaymentResponse? GetPayment(Guid id)
    {
        _payments.TryGetValue(id, out var payment);
        return payment;
    }
}
