namespace MercuryPay.LendingService.Domain;

public class Installment
{
    public DateTime DueDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; }

    public Installment(DateTime dueDate, decimal principalAmount, decimal interestAmount, decimal totalAmount, decimal paidAmount = 0, string status = "Pending")
    {
        DueDate = dueDate;
        PrincipalAmount = principalAmount;
        InterestAmount = interestAmount;
        TotalAmount = totalAmount;
        PaidAmount = paidAmount;
        Status = status;
    }

    // Required for EF Core
    protected Installment() 
    {
        Status = null!;
    }
}
