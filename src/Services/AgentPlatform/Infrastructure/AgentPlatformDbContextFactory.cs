using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MercuryPay.AgentPlatform.Infrastructure;

public sealed class AgentPlatformDbContextFactory : IDesignTimeDbContextFactory<AgentPlatformDbContext>
{
    public AgentPlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AgentPlatformDbContext>()
            .UseNpgsql("Host=localhost;Database=agentdb;Username=postgres;Password=postgres;Ssl Mode=Disable")
            .Options;

        return new AgentPlatformDbContext(options);
    }
}

