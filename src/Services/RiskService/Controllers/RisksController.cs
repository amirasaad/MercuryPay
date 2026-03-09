using Microsoft.AspNetCore.Mvc;
using MercuryPay.RiskService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.RiskService.Controllers;

/// <summary>
/// Risk Assessment Query API.
/// Provides endpoints to retrieve and inspect fraud risk evaluations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class RisksController(RiskDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Get risk assessment for a specific payment.
    /// </summary>
    /// <param name="paymentId">The payment ID to query</param>
    /// <returns>Risk assessment details or 404 if not found</returns>
    [HttpGet("{paymentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskAssessmentResponse>> GetRiskAssessment(string paymentId)
    {
        if (!Guid.TryParse(paymentId, out var guidPaymentId))
        {
            return BadRequest($"Invalid payment ID: {paymentId}");
        }

        var assessment = await dbContext.RiskAssessments
            .Where(a => a.PaymentId == guidPaymentId)
            .FirstOrDefaultAsync();

        if (assessment == null)
        {
            return NotFound($"No risk assessment found for payment {paymentId}");
        }

        return Ok(new RiskAssessmentResponse
        {
            Id = assessment.Id.ToString(),
            PaymentId = assessment.PaymentId.ToString(),
            RiskScore = assessment.RiskScore,
            IsApproved = assessment.IsApproved,
            Reason = assessment.Reason,
            CreatedAt = assessment.CreatedAt.ToString("O")
        });
    }

    /// <summary>
    /// List all risk assessments with pagination.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paged list of risk assessments</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskAssessmentsPageResponse>> ListRiskAssessments(int page = 1, int pageSize = 10)
    {
        if (page < 1)
        {
            return BadRequest("Page number must be >= 1");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("Page size must be between 1 and 100");
        }

        var query = dbContext.RiskAssessments.OrderByDescending(a => a.CreatedAt);
        
        var totalCount = await query.CountAsync();
        var assessments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new RiskAssessmentsPageResponse
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Assessments = assessments.Select(a => new RiskAssessmentResponse
            {
                Id = a.Id.ToString(),
                PaymentId = a.PaymentId.ToString(),
                RiskScore = a.RiskScore,
                IsApproved = a.IsApproved,
                Reason = a.Reason,
                CreatedAt = a.CreatedAt.ToString("O")
            }).ToList()
        });
    }

    /// <summary>
    /// Get statistics about risk assessments.
    /// </summary>
    /// <returns>Risk statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskStatisticsResponse>> GetRiskStatistics()
    {
        var totalAssessments = await dbContext.RiskAssessments.CountAsync();
        var approvedCount = await dbContext.RiskAssessments.CountAsync(a => a.IsApproved);
        var rejectedCount = await dbContext.RiskAssessments.CountAsync(a => !a.IsApproved);
        var averageRiskScore = await dbContext.RiskAssessments
            .AverageAsync(a => (double)a.RiskScore);

        return Ok(new RiskStatisticsResponse
        {
            TotalAssessments = totalAssessments,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount,
            ApprovalRate = totalAssessments > 0 ? (double)approvedCount / totalAssessments * 100 : 0,
            AverageRiskScore = Math.Round(averageRiskScore, 2)
        });
    }
}

/// <summary>
/// Risk assessment response model.
/// </summary>
public class RiskAssessmentResponse
{
    /// <summary>
    /// Unique identifier for this assessment.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The payment ID being assessed.
    /// </summary>
    public string PaymentId { get; set; } = string.Empty;

    /// <summary>
    /// Risk score from 0 (lowest) to 100 (highest).
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Whether the payment was approved or rejected.
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Human-readable explanation of the assessment.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the assessment was created.
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// Paged response for risk assessments.
/// </summary>
public class RiskAssessmentsPageResponse
{
    /// <summary>
    /// Total number of risk assessments in the system.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// List of assessments on this page.
    /// </summary>
    public List<RiskAssessmentResponse> Assessments { get; set; } = [];
}

/// <summary>
/// Risk statistics for monitoring and analytics.
/// </summary>
public class RiskStatisticsResponse
{
    /// <summary>
    /// Total number of assessments performed.
    /// </summary>
    public int TotalAssessments { get; set; }

    /// <summary>
    /// Number of approved payments.
    /// </summary>
    public int ApprovedCount { get; set; }

    /// <summary>
    /// Number of rejected payments.
    /// </summary>
    public int RejectedCount { get; set; }

    /// <summary>
    /// Approval rate as a percentage (0-100).
    /// </summary>
    public double ApprovalRate { get; set; }

    /// <summary>
    /// Average risk score across all assessments.
    /// </summary>
    public double AverageRiskScore { get; set; }
}
