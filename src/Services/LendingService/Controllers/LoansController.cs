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

        var loan = await _lendingService.CreateLoan(userId, request.Amount, request.Currency);
        
        return CreatedAtAction(nameof(Get), new { id = loan.Id }, new LoanResponse(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            loan.Status,
            loan.CreatedAt
        ));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var loan = await _lendingService.GetLoan(id);
        
        if (loan == null)
        {
            return NotFound();
        }

        return Ok(new LoanResponse(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            loan.Status,
            loan.CreatedAt
        ));
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var loans = await _lendingService.GetLoansByUser(userId);
        
        return Ok(loans.Select(loan => new LoanResponse(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            loan.Status,
            loan.CreatedAt
        )));
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryDisbursement(Guid id)
    {
        var result = await _lendingService.RetryDisbursement(id);
        
        if (!result)
        {
            return BadRequest("Cannot retry disbursement. Loan not found or status is not DisbursementFailed.");
        }

        return Ok("Disbursement retry initiated.");
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

public record CreateLoanRequest(string UserId, decimal Amount, string Currency);
public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt);
