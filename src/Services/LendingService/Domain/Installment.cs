namespace MercuryPay.LendingService.Domain;

public class Installment
{
    public DateTime DueDate { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal InterestAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string Status { get; private set; }

    public Installment(DateTime dueDate, decimal principalAmount, decimal interestAmount, decimal totalAmount, decimal paidAmount = 0, string status = "Pending")
    {
        DueDate = dueDate;
        PrincipalAmount = principalAmount;
        InterestAmount = interestAmount;
        TotalAmount = totalAmount;
        PaidAmount = paidAmount;
        Status = status;
    }

    public void MarkAsPaid()
    {
        PaidAmount = TotalAmount;
        Status = "Paid";
    }

    public void ApplyPayment(decimal amount)
    {
        PaidAmount += amount;
        Status = "PartiallyPaid";
    }

    public void Cancel()
    {
        Status = "Cancelled";
    }

    // Required for EF Core
    protected Installment() 
    {
        Status = null!;
    }
}
