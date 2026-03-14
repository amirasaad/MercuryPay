using System;
using System.Collections.Generic;
using System.Linq;

namespace MercuryPay.WalletService.Domain;

public class Wallet
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; }

    // EF Core navigation property
    public virtual ICollection<LedgerEntry> Ledger { get; private set; } = new List<LedgerEntry>();

    public Wallet(Guid id, string userId, string currency)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must not be empty.", nameof(currency));

        Id = id;
        UserId = userId;
        Currency = currency.Trim().ToUpperInvariant();
        Balance = 0;
    }

    // Required for EF Core
    private Wallet() 
    {
        UserId = default!;
        Currency = default!;
    }

    public void Credit(decimal amount, string transactionId, string description)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (Ledger.Any(x => x.TransactionId == transactionId)) return; // Idempotent: ignore duplicate

        Ledger.Add(new LedgerEntry(
            Guid.NewGuid(),
            Id,
            amount,
            transactionId,
            description,
            DateTimeOffset.UtcNow
        ));

        Balance += amount;
    }

    public void Debit(decimal amount, string transactionId, string description)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (Ledger.Any(x => x.TransactionId == transactionId)) return; // Idempotent: ignore duplicate
        if (Balance < amount) throw new InvalidOperationException("Insufficient funds");

        Ledger.Add(new LedgerEntry(
            Guid.NewGuid(),
            Id,
            -amount,
            transactionId,
            description,
            DateTimeOffset.UtcNow
        ));

        Balance -= amount;
    }
}

public class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public decimal Amount { get; private set; }
    public string TransactionId { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    public LedgerEntry(Guid id, Guid walletId, decimal amount, string transactionId, string description, DateTimeOffset timestamp)
    {
        Id = id;
        WalletId = walletId;
        Amount = amount;
        TransactionId = transactionId;
        Description = description;
        Timestamp = timestamp;
    }

    // Required for EF Core
    private LedgerEntry() 
    {
        TransactionId = default!;
        Description = default!;
    }
}
