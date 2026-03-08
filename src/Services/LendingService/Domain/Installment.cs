namespace MercuryPay.LendingService.Domain;

public record Installment(
    DateTime DueDate,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    string Status = "Pending"
);
