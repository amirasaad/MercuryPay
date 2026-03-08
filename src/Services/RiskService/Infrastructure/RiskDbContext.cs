using Microsoft.EntityFrameworkCore;
using MercuryPay.RiskService.Domain;
using MassTransit;

namespace MercuryPay.RiskService.Infrastructure;

public class RiskDbContext(DbContextOptions<RiskDbContext> options) : DbContext(options)
{
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<RiskAssessment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.PaymentId).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        // Configure MassTransit Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
