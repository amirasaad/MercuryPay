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
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var resolvedUserId = string.IsNullOrWhiteSpace(request.UserId) && !string.IsNullOrWhiteSpace(userId)
            ? userId
            : request.UserId;

        var wallet = _walletService.CreateWallet(resolvedUserId, request.Currency);
        return CreatedAtAction(nameof(Get), new { id = wallet.Id }, wallet);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetWallets()
    {
        var userId = HttpContext.Request.Query["userId"].ToString();
        if (string.IsNullOrEmpty(userId))
        {
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID not found in token or query");
        }

        var wallets = _walletService.GetWalletsByUserId(userId);
        if (!wallets.Any())
        {
            var created = _walletService.CreateWallet(userId, "USD");
            wallets = _walletService.GetWalletsByUserId(userId);
        }
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
