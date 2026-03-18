namespace MercuryPay.PaymentService.Models;

public record PaymentRequest(decimal Amount, string Currency, string FromUserId, string ToUserId, Guid? ReferenceId = null);

public record PaymentResponse(Guid Id, string Status, decimal Amount, string Currency, string FromUserId, string ToUserId, Guid? ReferenceId, string? RejectionReason = null);
