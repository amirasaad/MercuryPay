using System.Collections.Concurrent;
using MercuryPay.WalletService.Models;

namespace MercuryPay.WalletService.Services;

public interface IWalletService
{
    Wallet CreateWallet(string userId, string currency);
    Wallet? GetWallet(Guid id);
}

public class WalletService : IWalletService
{
    private static readonly ConcurrentDictionary<Guid, Wallet> _wallets = new();

    public Wallet CreateWallet(string userId, string currency)
    {
        var wallet = new Wallet(Guid.NewGuid(), userId, currency, 0.00m);
        _wallets[wallet.Id] = wallet;
        return wallet;
    }

    public Wallet? GetWallet(Guid id)
    {
        _wallets.TryGetValue(id, out var wallet);
        return wallet;
    }
}
