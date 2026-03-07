using MercuryPay.PaymentService.Models;
using MercuryPay.PaymentService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MercuryPay.PaymentService.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    private readonly IPaymentService _paymentService = paymentService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PaymentRequest request)
    {
        try
        {
            var response = await _paymentService.CreatePayment(request);
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var payment = await _paymentService.GetPayment(id);
        if (payment == null)
        {
            return NotFound();
        }
        return Ok(payment);
    }
}
