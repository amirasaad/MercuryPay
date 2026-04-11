using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MercuryPay.LendingService.Controllers;

[Authorize]
[ApiController]
[Route("loans")]
public class LoansController(ILendingService lendingService) : ControllerBase
{
    private readonly ILendingService _lendingService = lendingService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLoanRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be positive");
        }

        // Use authenticated user ID if available, otherwise fallback to request
        var userId = User.Identity?.IsAuthenticated == true 
            ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? request.UserId 
            : request.UserId;

        try
        {
            var loan = await _lendingService.CreateLoan(userId, request.Amount, request.Currency, request.TermMonths);
            return CreatedAtAction(nameof(Get), new { id = loan.Id }, MapToResponse(loan));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var loan = await _lendingService.GetLoan(id);
        
        if (loan == null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(loan));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyLoans()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var loans = await _lendingService.GetLoansByUser(userId);
        
        return Ok(loans.Select(MapToResponse));
    }

    [HttpGet("user/{userId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var loans = await _lendingService.GetLoansByUser(userId);
        
        return Ok(loans.Select(MapToResponse));
    }

    private static LoanResponse MapToResponse(Loan loan)
    {
        return new LoanResponse(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            loan.Status.ToString(),
            loan.CreatedAt,
            loan.TermMonths,
            loan.AnnualInterestRate,
            loan.RepaymentSchedule != null ? new RepaymentScheduleDto(
                [.. loan.RepaymentSchedule.Installments.Select(i => new InstallmentDto(
                    i.DueDate,
                    i.PrincipalAmount,
                    i.InterestAmount,
                    i.TotalAmount,
                    i.PaidAmount,
                    i.Status
                ))],
                loan.RepaymentSchedule.TotalInterest,
                loan.RepaymentSchedule.AnnualInterestRate
            ) : null
        );
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryDisbursement(Guid id)
    {
        var result = await _lendingService.RetryDisbursement(id);
        
        if (!result)
        {
            return BadRequest("Cannot retry disbursement");
        }
        
        return Accepted();
    }

    [HttpPost("{id}/repay")]
    public async Task<IActionResult> Repay(Guid id, [FromBody] RepayLoanRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be positive");
        }

        var result = await _lendingService.RepayLoan(id, request.Amount);
        
        if (!result)
        {
            return BadRequest("Loan cannot be repaid (invalid status or not found)");
        }

        return Accepted();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        return Ok(new
        {
            Id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Name = User.Identity?.Name,
            Claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}

public record CreateLoanRequest(string UserId, decimal Amount, string Currency, int TermMonths = 12);
public record RepayLoanRequest(decimal Amount);
public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, int TermMonths, decimal AnnualInterestRate, RepaymentScheduleDto? RepaymentSchedule = null);
public record InstallmentDto(DateTime DueDate, decimal PrincipalAmount, decimal InterestAmount, decimal TotalAmount, decimal PaidAmount, string Status);
public record RepaymentScheduleDto(List<InstallmentDto> Installments, decimal TotalInterest, decimal AnnualInterestRate);
