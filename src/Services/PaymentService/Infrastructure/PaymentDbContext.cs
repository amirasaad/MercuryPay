using Microsoft.EntityFrameworkCore;
using MercuryPay.PaymentService.Domain;
using MassTransit;

namespace MercuryPay.PaymentService.Infrastructure;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Payment>().HasKey(p => p.Id);
        modelBuilder.Entity<Payment>().Property(p => p.Id).ValueGeneratedNever();
        // Unique index on ReferenceId (nullable): enforces idempotency at the DB layer.
        // PostgreSQL unique indexes allow multiple NULLs, so payments without a ReferenceId are unaffected.
        modelBuilder.Entity<Payment>().HasIndex(p => p.ReferenceId).IsUnique().HasFilter("\"ReferenceId\" IS NOT NULL");
        
        // Configure MassTransit Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
