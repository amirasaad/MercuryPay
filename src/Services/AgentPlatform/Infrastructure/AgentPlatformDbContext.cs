using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.AgentPlatform.Infrastructure;

public sealed class AgentPlatformDbContext(DbContextOptions<AgentPlatformDbContext> options) : DbContext(options)
{
    public DbSet<RetryAttemptState> RetryAttempts => Set<RetryAttemptState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RetryAttemptState>(b =>
        {
            b.HasKey(x => x.CaptureAttemptId);
            b.Property(x => x.CaptureAttemptId).ValueGeneratedNever();
            b.Property(x => x.PaymentId).IsRequired();
            b.Property(x => x.RetryCount).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

public sealed class RetryAttemptState
{
    public Guid CaptureAttemptId { get; set; }
    public Guid PaymentId { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
