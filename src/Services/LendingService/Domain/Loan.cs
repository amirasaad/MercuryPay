namespace MercuryPay.LendingService.Domain;

public class Loan(Guid id, string userId, decimal amount, string currency, LoanStatus status, DateTime createdAt, int termMonths, decimal annualInterestRate)
{
    public Guid Id { get; private set; } = id;
    public string UserId { get; private set; } = userId;
    public decimal Amount { get; private set; } = amount > 100000m ? throw new ArgumentException("Amount exceeds maximum allowed") : amount <= 0m ? throw new ArgumentException("Amount must be positive.") : amount;
    public string Currency { get; private set; } = string.IsNullOrWhiteSpace(currency) ? throw new ArgumentException("Currency must not be empty.", nameof(currency)) : currency.Trim().ToUpperInvariant();
    public LoanStatus Status { get; private set; } = status;
    public DateTime CreatedAt { get; private set; } = createdAt;
    public int TermMonths { get; private set; } = termMonths <= 0 || termMonths > 120 ? throw new ArgumentOutOfRangeException(nameof(termMonths), "Term must be between 1 and 120 months.") : termMonths;
    public decimal AnnualInterestRate { get; private set; } = annualInterestRate <= 0m || annualInterestRate > 1m ? throw new ArgumentOutOfRangeException(nameof(annualInterestRate), "Annual interest rate must be a fractional decimal between 0 (exclusive) and 1.0 (inclusive), e.g. 0.05 for 5%.") : annualInterestRate;
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
        if (Status == LoanStatus.DisbursementFailed)
        {
            return;
        }

        if (Status != LoanStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot mark disbursement failed from status '{Status}'.");
        }

        Status = LoanStatus.DisbursementFailed;
    }

    public void MarkAsFraudDetected()
    {
        if (Status == LoanStatus.FraudDetected)
        {
            return;
        }

        if (Status == LoanStatus.Repaid)
        {
            throw new InvalidOperationException("Cannot mark a repaid loan as fraud detected.");
        }

        Status = LoanStatus.FraudDetected;
        CancelPendingInstallments();
    }

    public void RetryDisbursement()
    {
        if (Status != LoanStatus.DisbursementFailed)
        {
            throw new InvalidOperationException($"Cannot retry disbursement from status '{Status}'.");
        }

        Status = LoanStatus.Approved;
    }

    public void Approve()
    {
        if (Status == LoanStatus.Approved)
        {
            return;
        }

        if (Status != LoanStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot approve a loan from status '{Status}'.");
        }

        Status = LoanStatus.Approved;
    }

    public void MarkAsRepaymentProcessing()
    {
        if (Status != LoanStatus.Approved && Status != LoanStatus.Active)
        {
            throw new InvalidOperationException($"Cannot start repayment processing from status '{Status}'.");
        }

        Status = LoanStatus.RepaymentProcessing;
    }

    public void MarkAsRepaid()
    {
        Status = LoanStatus.Repaid;
    }

    public void MarkAsRepaymentFailed()
    {
        if (Status == LoanStatus.RepaymentFailed)
        {
            return;
        }

        if (Status != LoanStatus.RepaymentProcessing)
        {
            throw new InvalidOperationException($"Cannot mark repayment failed from status '{Status}'.");
        }

        Status = LoanStatus.RepaymentFailed;
    }

    public void ProcessRepayment(decimal amount)
    {
        if (RepaymentSchedule == null) return;

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Repayment amount must be positive.");
        }

        if (Status != LoanStatus.RepaymentProcessing)
        {
            throw new InvalidOperationException($"Cannot process repayment from status '{Status}'.");
        }

        var remainingPayment = amount;

        // Find pending installments ordered by date
        var pendingInstallments = RepaymentSchedule.Installments
            .Where(i => i.Status == "Pending" || i.Status == "PartiallyPaid")
            .OrderBy(i => i.DueDate)
            .ToList();

        foreach (var installment in pendingInstallments)
        {
            if (remainingPayment <= 0) break;

            var amountDue = installment.TotalAmount - installment.PaidAmount;
            
            if (remainingPayment >= amountDue)
            {
                // Full payment for this installment
                remainingPayment -= amountDue;
                installment.MarkAsPaid();
            }
            else
            {
                // Partial payment
                installment.ApplyPayment(remainingPayment);
                remainingPayment = 0;
            }
        }
        
        // Update loan status if all paid
        if (RepaymentSchedule.Installments.All(i => i.Status == "Paid"))
        {
            Status = LoanStatus.Repaid;
        }
        else
        {
            Status = LoanStatus.Active;
        }
    }

    public void CancelPendingInstallments()
    {
        if (RepaymentSchedule == null) return;
        foreach (var i in RepaymentSchedule.Installments)
        {
            if (i.Status == "Pending" || i.Status == "PartiallyPaid")
            {
                i.Cancel();
            }
        }
    }
}
