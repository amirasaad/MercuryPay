using MassTransit;
using MercuryPay.AgentPlatform.Agents;
using MercuryPay.AgentPlatform.Infrastructure;
using MercuryPay.BuildingBlocks.AgentPlatform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IAgentTaskMetricSink, LoggerAgentTaskMetricSink>();

var connectionString = builder.Configuration.GetConnectionString("agentdb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AgentPlatformDbContext>(options =>
        options.UseInMemoryDatabase("AgentPlatformDb"));
}
else
{
    if (!connectionString.Contains("Ssl Mode") && !connectionString.Contains("SslMode"))
    {
        builder.Configuration["ConnectionStrings:agentdb"] = $"{connectionString};Ssl Mode=Disable";
    }

    builder.AddNpgsqlDbContext<AgentPlatformDbContext>("agentdb", settings =>
        settings.DisableRetry = false);
}

builder.AddEventBus(x =>
{
    x.AddConsumer<RetryAgentConsumer>();
    x.AddConsumer<PaymentCreatedFaultConsumer>();

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        x.AddEntityFrameworkOutbox<AgentPlatformDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres();
            o.UseBusOutbox();
        });
    }
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/", () => "Agent Platform is running.");
app.MapGet("/env", () => app.Environment.EnvironmentName);
app.MapGet("/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
    string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));

await app.SafeMigrateAsync<AgentPlatformDbContext>();

app.Run();
