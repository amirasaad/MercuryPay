using System;
using MercuryPay.WalletService.Domain;
using MercuryPay.WalletService.Infrastructure;
using MercuryPay.WalletService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MercuryPay.WalletService.Tests;

/// <summary>
/// Unit and integration tests for domain-level invariants and service-level
/// duplicate/concurrency protections.
/// </summary>
public class WalletDomainTests
{
    // ── Domain unit tests ────────────────────────────────────────────────────

    [Fact]
    public void Wallet_Constructor_NormalizesCurrencyToUpperCase()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "usd");
        Assert.Equal("USD", wallet.Currency);
    }

    [Fact]
    public void Wallet_Constructor_ThrowsOnEmptyUserId()
    {
        Assert.Throws<ArgumentException>(() => new Wallet(Guid.NewGuid(), "", "USD"));
        Assert.Throws<ArgumentException>(() => new Wallet(Guid.NewGuid(), "   ", "USD"));
    }

    [Fact]
    public void Wallet_Constructor_ThrowsOnEmptyCurrency()
    {
        Assert.Throws<ArgumentException>(() => new Wallet(Guid.NewGuid(), "user_1", ""));
        Assert.Throws<ArgumentException>(() => new Wallet(Guid.NewGuid(), "user_1", "   "));
    }

    [Fact]
    public void Debit_ThrowsArgumentException_WhenAmountIsZeroOrNegative()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "USD");
        wallet.Credit(100m, "TX-INIT", "initial");

        Assert.Throws<ArgumentException>(() => wallet.Debit(0m, "TX-1", "zero"));
        Assert.Throws<ArgumentException>(() => wallet.Debit(-10m, "TX-2", "negative"));
    }

    [Fact]
    public void Credit_ThrowsArgumentException_WhenAmountIsZeroOrNegative()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "USD");

        Assert.Throws<ArgumentException>(() => wallet.Credit(0m, "TX-1", "zero"));
        Assert.Throws<ArgumentException>(() => wallet.Credit(-5m, "TX-2", "negative"));
    }

    [Fact]
    public void Debit_ThrowsInvalidOperationException_WhenInsufficientFunds()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "USD");
        wallet.Credit(50m, "TX-INIT", "initial");

        Assert.Throws<InvalidOperationException>(() => wallet.Debit(100m, "TX-1", "overspend"));
    }

    [Fact]
    public void DuplicateCredit_IsIdempotent_InMemory()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "USD");
        var txId = "TX-IDEMPOTENT";

        wallet.Credit(100m, txId, "first");
        wallet.Credit(100m, txId, "duplicate – should be ignored");

        Assert.Equal(100m, wallet.Balance);
        Assert.Single(wallet.Ledger);
    }

    [Fact]
    public void DuplicateDebit_IsIdempotent_InMemory()
    {
        var wallet = new Wallet(Guid.NewGuid(), "user_1", "USD");
        wallet.Credit(500m, "TX-INIT", "initial");
        var txId = "TX-DEBIT-ONCE";

        wallet.Debit(100m, txId, "first");
        wallet.Debit(100m, txId, "duplicate – should be ignored");

        Assert.Equal(400m, wallet.Balance);
        Assert.Equal(2, wallet.Ledger.Count); // INIT + 1 debit
    }

    // ── Service-level tests using in-memory DB ───────────────────────────────

    private static WalletService.Services.WalletService BuildService(string dbName)
    {
        var opts = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new WalletDbContext(opts);
        return new WalletService.Services.WalletService(ctx, NullLogger<WalletService.Services.WalletService>.Instance);
    }

    [Fact]
    public void CreateWallet_ThrowsInvalidOperation_WhenDuplicateUserAndCurrency()
    {
        var svc = BuildService($"dup_test_{Guid.NewGuid()}");

        svc.CreateWallet("user_dup", "USD");

        Assert.Throws<InvalidOperationException>(() => svc.CreateWallet("user_dup", "USD"));
    }

    [Fact]
    public void CreateWallet_CurrencyNormalization_PreventsCaseDuplicates()
    {
        var svc = BuildService($"dup_case_{Guid.NewGuid()}");

        svc.CreateWallet("user_case", "usd");

        // Creating with different-case currency should also be detected as duplicate
        Assert.Throws<InvalidOperationException>(() => svc.CreateWallet("user_case", "USD"));
    }

    [Fact]
    public void CreateWallet_ThrowsArgumentException_WhenUserIdIsEmpty()
    {
        var svc = BuildService($"empty_user_{Guid.NewGuid()}");

        Assert.Throws<ArgumentException>(() => svc.CreateWallet("", "USD"));
    }

    [Fact]
    public void CreateWallet_ThrowsArgumentException_WhenCurrencyIsEmpty()
    {
        var svc = BuildService($"empty_currency_{Guid.NewGuid()}");

        Assert.Throws<ArgumentException>(() => svc.CreateWallet("user_x", ""));
    }
}
