namespace MercuryPay.LendingService.Domain;

public enum LoanStatus
{
    Processing = 0,
    Approved = 1,
    DisbursementFailed = 2,
    FraudDetected = 3,
    Active = 4,
    RepaymentProcessing = 5,
    RepaymentFailed = 6,
    Repaid = 7
}

