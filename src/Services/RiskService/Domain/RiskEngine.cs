namespace MercuryPay.RiskService.Domain;

/// <summary>
/// Interface for evaluating risk rules.
/// </summary>
public interface IRule
{
    /// <summary>
    /// Rule name for logging and identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluate whether this rule applies to the given transaction.
    /// </summary>
    /// <returns>True if this rule applies, false otherwise.</returns>
    bool Applies(decimal amount, string fromUserId, string toUserId);

    /// <summary>
    /// Get the risk score contribution from this rule.
    /// </summary>
    int GetRiskScore();

    /// <summary>
    /// Get the human-readable reason for this rule's evaluation.
    /// </summary>
    string GetReason();
}

/// <summary>
/// Factory for creating rule sets.
/// </summary>
public interface IRuleEngineFactory
{
    /// <summary>
    /// Create the default set of rules.
    /// </summary>
    IEnumerable<IRule> CreateDefaultRules(decimal amount, string fromUserId, string toUserId);
}

/// <summary>
/// Risk evaluation engine using configurable rules.
/// </summary>
public interface IRiskEngine
{
    /// <summary>
    /// Evaluate risk for a transaction.
    /// </summary>
    RiskEvaluation Evaluate(decimal amount, string fromUserId, string toUserId);
}

/// <summary>
/// Result of a risk evaluation.
/// </summary>
public class RiskEvaluation
{
    public int RiskScore { get; set; }
    public bool IsApproved { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string[] TriggeredRules { get; set; } = [];
}

/// <summary>
/// Rule: Reject transactions above a threshold value.
/// </summary>
public class HighValueRule(decimal amount, decimal threshold = 10000m) : IRule
{
    private readonly decimal _threshold = threshold;
    private readonly decimal _amount = amount;

    public string Name => "High Value Transaction";

    public bool Applies(decimal amount, string fromUserId, string toUserId)
    {
        return _amount > _threshold;
    }

    public int GetRiskScore() => 90;

    public string GetReason() => $"Transaction amount (${_amount:F2}) exceeds high-value threshold (${_threshold:F2})";
}

/// <summary>
/// Rule: Reject transactions from suspicious user accounts.
/// </summary>
public class SuspiciousUserRule(string fromUserId, string suspiciousPrefix = "suspicious") : IRule
{
    private readonly string _fromUserId = fromUserId;
    private readonly string _suspiciousPrefix = suspiciousPrefix;

    public string Name => "Suspicious User Detection";

    public bool Applies(decimal amount, string fromUserId, string toUserId)
    {
        return _fromUserId.StartsWith(_suspiciousPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public int GetRiskScore() => 80;

    public string GetReason() => $"User '{_fromUserId}' matches suspicious user pattern";
}

/// <summary>
/// Rule: Default low-risk for standard transactions.
/// </summary>
public class DefaultLowRiskRule : IRule
{
    public string Name => "Default - Low Risk";

    public bool Applies(decimal amount, string fromUserId, string toUserId)
    {
        return true; // Always applies as fallback
    }

    public int GetRiskScore() => 10;

    public string GetReason() => "Transaction passed all risk checks - low risk";
}

/// <summary>
/// Default rule engine factory.
/// </summary>
public class DefaultRuleEngineFactory : IRuleEngineFactory
{
    public IEnumerable<IRule> CreateDefaultRules(decimal amount, string fromUserId, string toUserId)
    {
        return new IRule[]
        {
            new HighValueRule(amount),
            new SuspiciousUserRule(fromUserId),
            new DefaultLowRiskRule()
        };
    }
}

/// <summary>
/// Implementation of the risk evaluation engine.
/// </summary>
public class RiskEngine(IRuleEngineFactory? factory = null) : IRiskEngine
{
    private readonly IRuleEngineFactory _factory = factory ?? new DefaultRuleEngineFactory();

    public RiskEvaluation Evaluate(decimal amount, string fromUserId, string toUserId)
    {
        var rules = _factory.CreateDefaultRules(amount, fromUserId, toUserId);
        var triggeredRules = new List<string>();
        var maxRiskScore = 0;
        var reason = string.Empty;

        // Evaluate all rules and find the highest risk score
        foreach (var rule in rules)
        {
            if (rule.Applies(amount, fromUserId, toUserId))
            {
                triggeredRules.Add(rule.Name);
                var score = rule.GetRiskScore();

                if (score > maxRiskScore)
                {
                    maxRiskScore = score;
                    reason = rule.GetReason();
                }
            }
        }

        // Determine if approved based on risk score
        // Threshold: scores >= 75 are rejected
        var isApproved = maxRiskScore < 75;

        return new RiskEvaluation
        {
            RiskScore = maxRiskScore,
            IsApproved = isApproved,
            Reason = reason,
            TriggeredRules = [..triggeredRules]
        };
    }
}
