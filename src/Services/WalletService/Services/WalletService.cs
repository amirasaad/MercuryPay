using MercuryPay.WalletService.Models;
using MercuryPay.WalletService.Infrastructure;
using DomainWallet = MercuryPay.WalletService.Domain.Wallet;

namespace MercuryPay.WalletService.Services;

public interface IWalletService
{
    Wallet CreateWallet(string userId, string currency);
    Wallet? GetWallet(Guid id);
}

public class WalletService(WalletDbContext context) : IWalletService
{
    private readonly WalletDbContext _context = context;

    public Wallet CreateWallet(string userId, string currency)
    {
        var domainWallet = new DomainWallet(Guid.NewGuid(), userId, currency);
        
        _context.Wallets.Add(domainWallet);
        _context.SaveChanges();

        return new Wallet(domainWallet.Id, domainWallet.UserId, domainWallet.Currency, domainWallet.Balance);
    }

    public Wallet? GetWallet(Guid id)
    {
        var domainWallet = _context.Wallets.Find(id);
        
        if (domainWallet == null)
        {
            return null;
        }

        return new Wallet(domainWallet.Id, domainWallet.UserId, domainWallet.Currency, domainWallet.Balance);
    }
}
