namespace MercuryPay.WalletService.Domain;

/// <summary>
/// Thrown when a debit operation cannot be completed because the wallet does not hold
/// sufficient funds. Extends <see cref="InvalidOperationException"/> so that callers
/// already catching <see cref="InvalidOperationException"/> continue to work.
/// </summary>
public sealed class InsufficientFundsException(Guid walletId, decimal balance, decimal requested)
    : InvalidOperationException($"Insufficient funds in wallet '{walletId}': balance {balance} < requested {requested}.")
{
    public Guid WalletId { get; } = walletId;
    public decimal Balance { get; } = balance;
    public decimal Requested { get; } = requested;
}
