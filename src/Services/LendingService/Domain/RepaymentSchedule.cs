namespace MercuryPay.LendingService.Domain;

public class RepaymentSchedule
{
    public List<Installment> Installments { get; private set; } = [];
    public decimal TotalInterest { get; private set; }
    public decimal AnnualInterestRate { get; private set; }

    public RepaymentSchedule(List<Installment> installments, decimal totalInterest, decimal annualInterestRate)
    {
        Installments = installments;
        TotalInterest = totalInterest;
        AnnualInterestRate = annualInterestRate;
    }

    // EF Core constructor
    private RepaymentSchedule() { }
}
