using MercuryPay.WalletService.Models;
using MercuryPay.WalletService.Infrastructure;
using DomainWallet = MercuryPay.WalletService.Domain.Wallet;

namespace MercuryPay.WalletService.Services;

public interface IWalletService
{
    Wallet CreateWallet(string userId, string currency);
    Wallet? GetWallet(Guid id);
    IEnumerable<Wallet> GetWalletsByUserId(string userId);
    void CreditWallet(Guid id, decimal amount);
    void DebitWallet(Guid id, decimal amount);
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
        _logger.LogInformation("Creating wallet for user {UserId} with currency {Currency}", userId, currency);
        
        var domainWallet = new DomainWallet(Guid.NewGuid(), userId, currency);
        
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

    public void CreditWallet(Guid id, decimal amount)
    {
        _logger.LogInformation("Crediting wallet {WalletId} with amount {Amount}", id, amount);
        
        var domainWallet = _context.Wallets.Find(id);
        if (domainWallet == null) 
        {
            _logger.LogWarning("Wallet {WalletId} not found for credit operation", id);
            throw new KeyNotFoundException("Wallet not found");
        }

        domainWallet.Credit(amount, Guid.NewGuid().ToString(), "Manual Credit");
        _context.SaveChanges();
        
        _logger.LogInformation("Wallet {WalletId} credited successfully. New Balance: {Balance}", id, domainWallet.Balance);
    }

    public void DebitWallet(Guid id, decimal amount)
    {
        _logger.LogInformation("Debiting wallet {WalletId} with amount {Amount}", id, amount);
        
        var domainWallet = _context.Wallets.Find(id);
        if (domainWallet == null) 
        {
            _logger.LogWarning("Wallet {WalletId} not found for debit operation", id);
            throw new KeyNotFoundException("Wallet not found");
        }

        domainWallet.Debit(amount, Guid.NewGuid().ToString(), "Manual Debit");
        _context.SaveChanges();
        
        _logger.LogInformation("Wallet {WalletId} debited successfully. New Balance: {Balance}", id, domainWallet.Balance);
    }
}
