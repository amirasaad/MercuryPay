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
                dueDate: CreatedAt.AddMonths(i),
                principalAmount: principalPayment,
                interestAmount: interestPayment,
                totalAmount: monthlyPayment,
                status: "Pending"
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

        var remainingPayment = amount;

        // Find pending installments ordered by date
        var pendingInstallments = RepaymentSchedule.Installments
            .Where(i => i.Status == "Pending" || i.Status == "PartiallyPaid")
            .OrderBy(i => i.DueDate)
            .ToList();

        foreach (var installment in pendingInstallments)
        {
            if (remainingPayment <= 0) break;

            var index = RepaymentSchedule.Installments.IndexOf(installment);
            var amountDue = installment.TotalAmount - installment.PaidAmount;
            
            if (remainingPayment >= amountDue)
            {
                // Full payment for this installment
                installment.PaidAmount = installment.TotalAmount;
                installment.Status = "Paid";
                remainingPayment -= amountDue;
            }
            else
            {
                // Partial payment
                installment.PaidAmount += remainingPayment;
                installment.Status = "PartiallyPaid";
                remainingPayment = 0;
            }
        }
        
        // Update loan status if all paid
        if (RepaymentSchedule.Installments.All(i => i.Status == "Paid"))
        {
            Status = "Repaid";
        }
        else
        {
            Status = "Active"; 
        }
    }
}
