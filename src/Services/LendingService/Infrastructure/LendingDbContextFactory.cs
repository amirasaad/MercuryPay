using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MercuryPay.LendingService.Infrastructure;

public class LendingDbContextFactory : IDesignTimeDbContextFactory<LendingDbContext>
{
    public LendingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LendingDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=LendingDb;Username=postgres;Password=postgres");

        return new LendingDbContext(optionsBuilder.Options);
    }
}
