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
    x.AddConsumer<FraudEvaluatedConsumer>();

    var connectionString = builder.Configuration.GetConnectionString("lendingdb");
    if (!string.IsNullOrEmpty(connectionString))
    {
        x.AddEntityFrameworkOutbox<LendingDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UseBusOutbox();
            o.UsePostgres();
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
    builder.Services.AddDbContext<LendingDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(15, TimeSpan.FromSeconds(3), null);
        }));
}

// Add Services
builder.Services.AddScoped<ILendingService, LendingService>();

// Cleanup service removed

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure Database is Created
await app.SafeMigrateAsync<LendingDbContext>();

app.Run();

public partial class Program { }
