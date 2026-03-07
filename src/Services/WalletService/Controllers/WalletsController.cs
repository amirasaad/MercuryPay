using MercuryPay.WalletService.Models;
using MercuryPay.WalletService.Services;
using Microsoft.AspNetCore.Mvc;

namespace MercuryPay.WalletService.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletsController(IWalletService walletService) : ControllerBase
{
    private readonly IWalletService _walletService = walletService;

    [HttpPost]
    public IActionResult Create([FromBody] CreateWalletRequest request)
    {
        var wallet = _walletService.CreateWallet(request.UserId, request.Currency);
        return CreatedAtAction(nameof(Get), new { id = wallet.Id }, wallet);
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var wallet = _walletService.GetWallet(id);
        if (wallet == null)
        {
            return NotFound();
        }
        return Ok(wallet);
    }
}
