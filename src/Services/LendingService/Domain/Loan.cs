namespace MercuryPay.LendingService.Domain;

public class Loan(Guid id, string userId, decimal amount, string currency, string status, DateTime createdAt, int termMonths, decimal annualInterestRate)
{
    public Guid Id { get; private set; } = id;
    public string UserId { get; private set; } = userId;
    public decimal Amount { get; private set; } = amount;
    public string Currency { get; private set; } = currency;
    public string Status { get; private set; } = status;
    public DateTime CreatedAt { get; private set; } = createdAt;
    public int TermMonths { get; private set; } = termMonths;
    public decimal AnnualInterestRate { get; private set; } = annualInterestRate;
    public RepaymentSchedule? RepaymentSchedule { get; private set; }

    public void GenerateRepaymentSchedule()
    {
        var installments = new List<Installment>();
        var monthlyRate = AnnualInterestRate / 12;
        var monthlyPayment = (Amount * monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), TermMonths)) /
                             ((decimal)Math.Pow((double)(1 + monthlyRate), TermMonths) - 1);
        
        decimal remainingBalance = Amount;
        decimal totalInterest = 0;

        for (int i = 1; i <= TermMonths; i++)
        {
            var interestPayment = remainingBalance * monthlyRate;
            var principalPayment = monthlyPayment - interestPayment;
            
            // Adjust for last payment to cover remaining balance exactly
            if (i == TermMonths)
            {
                principalPayment = remainingBalance;
                monthlyPayment = principalPayment + interestPayment;
            }

            remainingBalance -= principalPayment;
            totalInterest += interestPayment;

            installments.Add(new Installment(
                DueDate: CreatedAt.AddMonths(i),
                PrincipalAmount: principalPayment,
                InterestAmount: interestPayment,
                TotalAmount: monthlyPayment,
                Status: "Pending"
            ));
        }

        RepaymentSchedule = new RepaymentSchedule(installments, totalInterest, AnnualInterestRate);
    }

    public void MarkAsDisbursementFailed()
    {
        Status = "DisbursementFailed";
    }

    public void RetryDisbursement()
    {
        Status = "Approved";
    }

    public void Approve()
    {
        Status = "Approved";
    }

    public void MarkAsRepaymentProcessing()
    {
        Status = "RepaymentProcessing";
    }

    public void MarkAsRepaid()
    {
        Status = "Repaid";
    }

    public void MarkAsRepaymentFailed()
    {
        Status = "RepaymentFailed";
    }

    public void ProcessRepayment(decimal amount)
    {
        if (RepaymentSchedule == null) return;

        // Find the oldest pending installment
        var installment = RepaymentSchedule.Installments
            .OrderBy(i => i.DueDate)
            .FirstOrDefault(i => i.Status == "Pending");

        if (installment != null)
        {
            // For MVP, we assume the payment covers the installment if it's close enough
            // In a real app, we'd handle partial payments or verify exact amount
            if (amount >= installment.TotalAmount * 0.99m) // Allow small tolerance
            {
                var index = RepaymentSchedule.Installments.IndexOf(installment);
                RepaymentSchedule.Installments[index] = installment with { Status = "Paid" };
            }
        }

        // Check if all installments are paid
        if (RepaymentSchedule.Installments.All(i => i.Status == "Paid"))
        {
            Status = "Repaid";
        }
        else
        {
            // If not all paid, revert status to Approved (or Active) if it was RepaymentProcessing
            if (Status == "RepaymentProcessing")
            {
                Status = "Approved";
            }
        }
    }
}
