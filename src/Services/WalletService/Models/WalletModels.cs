namespace MercuryPay.WalletService.Models;

public record Wallet(Guid Id, string UserId, string Currency, decimal Balance);

/// <summary>
/// The wallet owner's user ID. When <c>null</c> or omitted by the caller, the
/// controller auto-populates it from the authenticated user's JWT claims
/// (<c>ClaimTypes.NameIdentifier</c>). Callers may not specify a UserId that
/// differs from their own identity — doing so results in a 403 Forbidden.
/// </summary>
public record CreateWalletRequest(string? UserId, string Currency);

public record CreditWalletRequest(decimal Amount, string TransactionId, string Description);
