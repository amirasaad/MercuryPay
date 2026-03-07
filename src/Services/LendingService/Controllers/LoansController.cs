using MercuryPay.LendingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MercuryPay.LendingService.Controllers;

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

        var loan = await _lendingService.CreateLoan(request.UserId, request.Amount, request.Currency);
        
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
}

public record CreateLoanRequest(string UserId, decimal Amount, string Currency);
public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt);
