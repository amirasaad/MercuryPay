using System.Diagnostics.Metrics;

namespace MercuryPay.LendingService.Metrics;

public static class LendingServiceMetrics
{
    public const string MeterName = "MercuryPay.LendingService";
    
    private static readonly Meter Meter = new(MeterName);
    
    public static readonly Counter<long> DisbursementFailures = Meter.CreateCounter<long>(
        "loan_disbursement_failures",
        description: "Number of failed loan disbursements");
}
