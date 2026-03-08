using MercuryPay.LendingService.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Infrastructure;

public class LendingDbContext(DbContextOptions<LendingDbContext> options) : DbContext(options)
{
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Loan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.AnnualInterestRate).HasPrecision(6, 4);

            entity.OwnsOne(e => e.RepaymentSchedule, schedule =>
            {
                schedule.Property(s => s.TotalInterest).HasPrecision(18, 2);
                schedule.Property(s => s.AnnualInterestRate).HasPrecision(6, 4);
                schedule.OwnsMany(s => s.Installments, installment =>
                {
                    installment.WithOwner().HasForeignKey("LoanId");
                    installment.Property<int>("Id");
                    installment.HasKey("LoanId", "Id");
                    installment.Property(i => i.PrincipalAmount).HasPrecision(18, 2);
                    installment.Property(i => i.InterestAmount).HasPrecision(18, 2);
                    installment.Property(i => i.TotalAmount).HasPrecision(18, 2);
                });
            });
        });
    }
}
