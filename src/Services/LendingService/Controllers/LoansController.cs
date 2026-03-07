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
            loan.Status
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
            loan.Status
        ));
    }
}

public record CreateLoanRequest(string UserId, decimal Amount, string Currency);
public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status);
