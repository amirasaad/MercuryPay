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
        var claimsUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claimsUserId))
            return BadRequest("UserId is required.");

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            // Auto-fill the UserId from the authenticated user's claims when not provided
            request = request with { UserId = claimsUserId };
        }
        else if (!string.Equals(request.UserId, claimsUserId, StringComparison.Ordinal))
        {
            // Prevent privilege escalation: callers may not create wallets for other users
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
            return BadRequest("Currency is required.");

        try
        {
            var wallet = _walletService.CreateWallet(request.UserId, request.Currency);
            return CreatedAtAction(nameof(Get), new { id = wallet.Id }, wallet);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet]
    public IActionResult GetWallets()
    {
        var requestingUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(requestingUserId))
            return Unauthorized();

        // Allow an explicit userId query param only if it matches the authenticated user
        var queryUserId = HttpContext.Request.Query["userId"].ToString();
        var userId = string.IsNullOrEmpty(queryUserId) ? requestingUserId : queryUserId;

        if (userId != requestingUserId)
            return Forbid();

        var wallets = _walletService.GetWalletsByUserId(userId);
        return Ok(wallets);
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var wallet = _walletService.GetWallet(id);
        if (wallet == null)
            return NotFound();

        // Enforce ownership: only the wallet owner may read it
        var requestingUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(requestingUserId))
            return Unauthorized();
        if (wallet.UserId != requestingUserId)
            return Forbid();

        return Ok(wallet);
    }

    [HttpPost("{id}/credit")]
    public IActionResult Credit(Guid id, [FromBody] CreditWalletRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return BadRequest("TransactionId is required.");
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Description is required.");

        try
        {
            _walletService.CreditWallet(id, request.Amount, request.TransactionId, request.Description);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
