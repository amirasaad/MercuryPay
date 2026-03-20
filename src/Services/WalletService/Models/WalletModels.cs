namespace MercuryPay.WalletService.Models;

public record Wallet(Guid Id, string UserId, string Currency, decimal Balance);

public record CreateWalletRequest(string? UserId, string Currency);

public record CreditWalletRequest(decimal Amount, string TransactionId, string Description);
