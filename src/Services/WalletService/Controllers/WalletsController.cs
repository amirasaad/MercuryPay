using MercuryPay.WalletService.Models;
using MercuryPay.WalletService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MercuryPay.WalletService.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class WalletsController(IWalletService walletService) : ControllerBase
{
    private readonly IWalletService _walletService = walletService;

    [HttpPost]
    public IActionResult Create([FromBody] CreateWalletRequest request)
    {
        // Override UserId with authenticated user
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var wallet = _walletService.CreateWallet(userId ?? request.UserId, request.Currency);
        return CreatedAtAction(nameof(Get), new { id = wallet.Id }, wallet);
    }

    [HttpGet]
    public IActionResult GetWallets()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID not found in token");
        }

        var wallets = _walletService.GetWalletsByUserId(userId);
        return Ok(wallets);
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

    [HttpPost("{id}/credit")]
    public IActionResult Credit(Guid id, [FromBody] decimal amount)
    {
        try
        {
            _walletService.CreditWallet(id, amount);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
