using MercuryPay.PaymentService.Models;

namespace MercuryPay.PaymentService.Services;

public interface IPaymentService
{
    PaymentResponse CreatePayment(PaymentRequest request);
}

public class PaymentService : IPaymentService
{
    public PaymentResponse CreatePayment(PaymentRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be positive");
        }

        // In a real application, this would save to a database.
        return new PaymentResponse(
            Guid.NewGuid(),
            "Pending",
            request.Amount,
            request.Currency,
            request.FromUserId,
            request.ToUserId
        );
    }
}
