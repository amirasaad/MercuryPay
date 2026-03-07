namespace MercuryPay.WalletService.Domain;

public class Wallet
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public string Currency { get; private set; }
    public decimal Balance { get; private set; }

    private readonly List<LedgerEntry> _ledger = new();
    public IReadOnlyCollection<LedgerEntry> Ledger => _ledger.AsReadOnly();

    public Wallet(Guid id, string userId, string currency)
    {
        Id = id;
        UserId = userId;
        Currency = currency;
        Balance = 0;
    }

    // Required for EF Core
    private Wallet() { }

    public void Credit(decimal amount, string transactionId, string description)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
        if (_ledger.Any(x => x.TransactionId == transactionId)) return; // Idempotent: ignore duplicate

        _ledger.Add(new LedgerEntry(
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
        if (_ledger.Any(x => x.TransactionId == transactionId)) return; // Idempotent: ignore duplicate
        if (Balance < amount) throw new InvalidOperationException("Insufficient funds");

        _ledger.Add(new LedgerEntry(
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
    private LedgerEntry() { }
}
