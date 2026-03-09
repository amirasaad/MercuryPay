using MercuryPay.RiskService.Domain;
using Xunit;

namespace MercuryPay.RiskService.Tests.Domain;

public class RiskEngineTests
{
    private readonly RiskEngine _engine;

    public RiskEngineTests()
    {
        _engine = new RiskEngine();
    }

    [Fact]
    public void Evaluate_LowValueTransaction_ShouldApprove()
    {
        // Arrange
        const decimal amount = 100m;
        const string fromUserId = "user_alice";
        const string toUserId = "user_bob";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.True(result.IsApproved);
        Assert.Equal(10, result.RiskScore); // Default low-risk score
        Assert.NotNull(result.Reason);
        Assert.Contains("low risk", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_HighValueTransaction_ShouldReject()
    {
        // Arrange
        const decimal amount = 15000m; // > 10,000
        const string fromUserId = "user_charlie";
        const string toUserId = "user_diana";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.False(result.IsApproved);
        Assert.Equal(90, result.RiskScore);
        Assert.NotNull(result.Reason);
        Assert.Contains("high-value", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_SuspiciousUser_ShouldReject()
    {
        // Arrange
        const decimal amount = 500m;
        const string fromUserId = "suspicious_actor_001";
        const string toUserId = "user_eve";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.False(result.IsApproved);
        Assert.Equal(80, result.RiskScore);
        Assert.NotNull(result.Reason);
        Assert.Contains("suspicious", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_BoundaryValueTransaction_ShouldApprove()
    {
        // Arrange: Test boundary condition (exactly at threshold)
        const decimal amount = 10000m; // Exactly at threshold, not > 10000
        const string fromUserId = "user_frank";
        const string toUserId = "user_grace";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert: Should be approved because > threshold is false
        Assert.True(result.IsApproved);
        Assert.Equal(10, result.RiskScore);
    }

    [Fact]
    public void Evaluate_JustAboveThreshold_ShouldReject()
    {
        // Arrange: Just above threshold
        const decimal amount = 10001m;
        const string fromUserId = "user_henry";
        const string toUserId = "user_iris";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.False(result.IsApproved);
        Assert.Equal(90, result.RiskScore);
    }

    [Fact]
    public void Evaluate_MultipleRulesTriggered_ShouldUseHighestScore()
    {
        // Arrange: High-value transaction from suspicious user
        const decimal amount = 15000m;
        const string fromUserId = "suspicious_big_spender";
        const string toUserId = "user_jack";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert: Should use highest score (90 from high-value, not 80 from suspicious)
        Assert.False(result.IsApproved);
        Assert.Equal(90, result.RiskScore); // High Value = 90, Suspicious = 80
        Assert.Equal(3, result.TriggeredRules.Length); // High Value, Suspicious, Default Low Risk
    }

    [Fact]
    public void Evaluate_CaseSensitivityCheck_ShouldDetectSuspiciousPrefixCaseInsensitive()
    {
        // Arrange: Test case-insensitive matching
        const decimal amount = 500m;
        const string fromUserId = "SUSPICIOUS_User123"; // Uppercase SUSPICIOUS
        const string toUserId = "user_kate";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.False(result.IsApproved);
        Assert.Contains("Suspicious User Detection", result.TriggeredRules);
    }

    [Fact]
    public void Evaluate_EmptyFromUserId_ShouldNotTriggerSuspiciousRule()
    {
        // Arrange
        const decimal amount = 500m;
        const string fromUserId = "";
        const string toUserId = "user_leo";

        // Act
        var result = _engine.Evaluate(amount, fromUserId, toUserId);

        // Assert
        Assert.True(result.IsApproved);
        Assert.DoesNotContain("Suspicious User Detection", result.TriggeredRules);
    }
}

public class HighValueRuleTests
{
    [Theory]
    [InlineData(9999.99)]
    [InlineData(10000)]
    public void Applies_BelowOrAtThreshold_ShouldNotApply(decimal amount)
    {
        // Act
        var rule = new HighValueRule(amount);
        var applies = rule.Applies(amount, "user", "user");

        // Assert
        Assert.False(applies);
    }

    [Theory]
    [InlineData(10000.01)]
    [InlineData(50000)]
    public void Applies_AboveThreshold_ShouldApply(decimal amount)
    {
        // Act
        var rule = new HighValueRule(amount);
        var applies = rule.Applies(amount, "user", "user");

        // Assert
        Assert.True(applies);
    }

    [Fact]
    public void GetRiskScore_ShouldReturn90()
    {
        // Act
        var rule = new HighValueRule(15000);

        // Assert
        Assert.Equal(90, rule.GetRiskScore());
    }
}

public class SuspiciousUserRuleTests
{
    [Theory]
    [InlineData("suspicious_actor")]
    [InlineData("suspicious_001")]
    [InlineData("SUSPICIOUS_user")]
    public void Applies_WithSuspiciousPrefix_ShouldApply(string fromUserId)
    {
        // Act
        var rule = new SuspiciousUserRule(fromUserId);
        var applies = rule.Applies(0, fromUserId, "any");

        // Assert
        Assert.True(applies);
    }

    [Theory]
    [InlineData("user_123")]
    [InlineData("john_suspect")]
    [InlineData("notsuspicious")]
    public void Applies_WithoutSuspiciousPrefix_ShouldNotApply(string fromUserId)
    {
        // Act
        var rule = new SuspiciousUserRule(fromUserId);
        var applies = rule.Applies(0, fromUserId, "any");

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void GetRiskScore_ShouldReturn80()
    {
        // Act
        var rule = new SuspiciousUserRule("suspicious_user");

        // Assert
        Assert.Equal(80, rule.GetRiskScore());
    }
}

public class DefaultLowRiskRuleTests
{
    [Fact]
    public void Applies_AlwaysTrue()
    {
        // Act
        var rule = new DefaultLowRiskRule();

        // Assert
        Assert.True(rule.Applies(0, "", ""));
        Assert.True(rule.Applies(999999, "any", "any"));
    }

    [Fact]
    public void GetRiskScore_ShouldReturn10()
    {
        // Act
        var rule = new DefaultLowRiskRule();

        // Assert
        Assert.Equal(10, rule.GetRiskScore());
    }
}
