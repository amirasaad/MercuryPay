using Microsoft.EntityFrameworkCore;
using MercuryPay.WalletService.Domain;

namespace MercuryPay.WalletService.Infrastructure;

public class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>().HasKey(w => w.Id);
        modelBuilder.Entity<Wallet>().Property(w => w.Id).ValueGeneratedNever();
        
        // LedgerEntry is owned by Wallet
        modelBuilder.Entity<Wallet>()
            .HasMany(w => w.Ledger)
            .WithOne()
            .HasForeignKey(l => l.WalletId);
            
        modelBuilder.Entity<LedgerEntry>().HasKey(l => l.Id);
        modelBuilder.Entity<LedgerEntry>().Property(l => l.Id).ValueGeneratedNever();
    }
}
