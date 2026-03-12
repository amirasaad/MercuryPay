using Microsoft.EntityFrameworkCore;
using MercuryPay.WalletService.Domain;
using MassTransit;

namespace MercuryPay.WalletService.Infrastructure;

public class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>().HasKey(w => w.Id);
        modelBuilder.Entity<Wallet>().Property(w => w.Id).ValueGeneratedNever();

        // Optimistic concurrency: xmin is the PostgreSQL row-version column
        modelBuilder.Entity<Wallet>()
            .Property(w => w.RowVersion)
            .IsRowVersion();

        // Enforce unique wallet per user/currency pair
        modelBuilder.Entity<Wallet>()
            .HasIndex(w => new { w.UserId, w.Currency })
            .IsUnique();

        // LedgerEntry is owned by Wallet
        modelBuilder.Entity<Wallet>()
            .HasMany(w => w.Ledger)
            .WithOne()
            .HasForeignKey(l => l.WalletId);
            
        modelBuilder.Entity<LedgerEntry>().HasKey(l => l.Id);
        modelBuilder.Entity<LedgerEntry>().Property(l => l.Id).ValueGeneratedNever();

        // Database-backed idempotency: each (WalletId, TransactionId) must be unique
        modelBuilder.Entity<LedgerEntry>()
            .HasIndex(l => new { l.WalletId, l.TransactionId })
            .IsUnique();

        // Configure MassTransit Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
