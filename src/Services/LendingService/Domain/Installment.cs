namespace MercuryPay.LendingService.Domain;

public record Installment(
    DateTime DueDate,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal PaidAmount = 0,
    string Status = "Pending"
);
