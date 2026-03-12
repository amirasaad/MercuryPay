using MercuryPay.WalletService.Models;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using DomainWallet = MercuryPay.WalletService.Domain.Wallet;

namespace MercuryPay.WalletService.Services;

public interface IWalletService
{
    Wallet CreateWallet(string userId, string currency);
    Wallet? GetWallet(Guid id);
    IEnumerable<Wallet> GetWalletsByUserId(string userId);
    void CreditWallet(Guid id, decimal amount, string transactionId, string description);
    void DebitWallet(Guid id, decimal amount);
    Task DebitWalletAsync(Guid id, decimal amount, bool saveChanges = true);
}

public class WalletService(WalletDbContext context, ILogger<WalletService> logger) : IWalletService
{
    private readonly WalletDbContext _context = context;
    private readonly ILogger<WalletService> _logger = logger;

    public IEnumerable<Wallet> GetWalletsByUserId(string userId)
    {
        return _context.Wallets
            .Where(w => w.UserId == userId)
            .Select(w => new Wallet(w.Id, w.UserId, w.Currency, w.Balance))
            .ToList();
    }

    public Wallet CreateWallet(string userId, string currency)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must not be empty.", nameof(currency));

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        _logger.LogInformation("Creating wallet for user {UserId} with currency {Currency}", userId, normalizedCurrency);

        // Prevent duplicate wallet for the same user/currency
        var existing = _context.Wallets
            .FirstOrDefault(w => w.UserId == userId && w.Currency == normalizedCurrency);
        if (existing != null)
            throw new InvalidOperationException($"A wallet for user '{userId}' with currency '{normalizedCurrency}' already exists.");

        var domainWallet = new DomainWallet(Guid.NewGuid(), userId, normalizedCurrency);
        
        _context.Wallets.Add(domainWallet);
        _context.SaveChanges();

        _logger.LogInformation("Wallet {WalletId} created successfully", domainWallet.Id);

        return new Wallet(domainWallet.Id, domainWallet.UserId, domainWallet.Currency, domainWallet.Balance);
    }

    public Wallet? GetWallet(Guid id)
    {
        var domainWallet = _context.Wallets.Find(id);
        
        if (domainWallet == null)
        {
            _logger.LogWarning("Wallet {WalletId} not found", id);
            return null;
        }

        return new Wallet(domainWallet.Id, domainWallet.UserId, domainWallet.Currency, domainWallet.Balance);
    }

    public void CreditWallet(Guid id, decimal amount, string transactionId, string description)
    {
        _logger.LogInformation("Crediting wallet {WalletId} with amount {Amount}", id, amount);
        
        var domainWallet = _context.Wallets.Include(w => w.Ledger).FirstOrDefault(w => w.Id == id);
        if (domainWallet == null) 
        {
            _logger.LogWarning("Wallet {WalletId} not found for credit operation", id);
            throw new KeyNotFoundException("Wallet not found");
        }

        domainWallet.Credit(amount, transactionId, description);
        _context.SaveChanges();
        
        _logger.LogInformation("Wallet {WalletId} credited successfully. New Balance: {Balance}", id, domainWallet.Balance);
    }

    public void DebitWallet(Guid id, decimal amount)
    {
        _logger.LogInformation("Debiting wallet {WalletId} with amount {Amount}", id, amount);
        
        var domainWallet = _context.Wallets.Include(w => w.Ledger).FirstOrDefault(w => w.Id == id);
        if (domainWallet == null) 
        {
            _logger.LogWarning("Wallet {WalletId} not found for debit operation", id);
            throw new KeyNotFoundException("Wallet not found");
        }

        domainWallet.Debit(amount, Guid.NewGuid().ToString(), "Manual Debit");
        _context.SaveChanges();
        
        _logger.LogInformation("Wallet {WalletId} debited successfully. New Balance: {Balance}", id, domainWallet.Balance);
    }

    public async Task DebitWalletAsync(Guid id, decimal amount, bool saveChanges = true)
    {
        _logger.LogInformation("Debiting wallet {WalletId} with amount {Amount}", id, amount);
        
        var domainWallet = await _context.Wallets.Include(w => w.Ledger).FirstOrDefaultAsync(w => w.Id == id);
        if (domainWallet == null) 
        {
            _logger.LogWarning("Wallet {WalletId} not found for debit operation", id);
            throw new KeyNotFoundException("Wallet not found");
        }

        domainWallet.Debit(amount, Guid.NewGuid().ToString(), "Manual Debit");
        
        if (saveChanges)
        {
            await _context.SaveChangesAsync();
        }
        
        _logger.LogInformation("Wallet {WalletId} debited successfully. New Balance: {Balance}", id, domainWallet.Balance);
    }
}
