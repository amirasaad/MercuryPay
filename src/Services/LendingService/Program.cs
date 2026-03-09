using MercuryPay.LendingService.Controllers;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Services;
using MercuryPay.LendingService.Consumers;
using MercuryPay.LendingService.Metrics;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add custom metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(LendingServiceMetrics.MeterName));

// Add Event Bus
builder.AddEventBus(x => 
{
    x.AddConsumer<LoanApprovedFaultConsumer>();
    x.AddConsumer<LoanCreatedConsumer>();
    x.AddConsumer<LoanRepaymentProcessedConsumer>();

    var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
    if (!string.IsNullOrEmpty(messagingConnectionString))
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(messagingConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    }
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add EF Core
var connectionString = builder.Configuration.GetConnectionString("lendingdb");
if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<LendingDbContext>(options =>
        options.UseInMemoryDatabase("LendingDb"));
}
else
{
    if (!connectionString.Contains("Ssl Mode") && !connectionString.Contains("SslMode"))
    {
        builder.Configuration["ConnectionStrings:lendingdb"] = $"{connectionString};Ssl Mode=Disable";
        connectionString = builder.Configuration.GetConnectionString("lendingdb");
    }

    builder.Services.AddDbContext<LendingDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(15, TimeSpan.FromSeconds(3), null);
        }));
}

// Add Services
builder.Services.AddScoped<ILendingService, LendingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure Database is Created
await app.SafeMigrateAsync<LendingDbContext>();

app.Run();

public partial class Program { }
