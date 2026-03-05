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
    public IActionResult Create([FromBody] PaymentRequest request)
    {
        var response = _paymentService.CreatePayment(request);
        return CreatedAtAction(nameof(Create), new { id = response.Id }, response);
    }
}
