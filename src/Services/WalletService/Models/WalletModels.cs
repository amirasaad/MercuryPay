namespace MercuryPay.WalletService.Models;

public record Wallet(Guid Id, string UserId, string Currency, decimal Balance);

/// <summary>
/// Optional wallet owner's user ID provided by the caller.
/// When omitted (or <c>null</c>), the controller fills it from the authenticated user's identity.
/// Must be <c>null</c> or equal to the authenticated user's ID; providing another user's ID returns 403.
/// </summary>
public record CreateWalletRequest(string? UserId, string Currency);

public record CreditWalletRequest(decimal Amount, string TransactionId, string Description);
