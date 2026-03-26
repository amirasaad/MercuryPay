using MercuryPay.WalletService.Services;
using MercuryPay.WalletService.Infrastructure;
using MercuryPay.WalletService.Consumers;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("walletdb");
if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<WalletDbContext>(options =>
        options.UseInMemoryDatabase("WalletDb"));
}
else
{
    // Force disable SSL for local development with Aspire
    if (!connectionString.Contains("Ssl Mode") && !connectionString.Contains("SslMode"))
    {
        builder.Configuration["ConnectionStrings:walletdb"] = $"{connectionString};Ssl Mode=Disable";
    }

    builder.AddNpgsqlDbContext<WalletDbContext>("walletdb", settings => 
        settings.DisableRetry = false); // Enable retry for resilience
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.AddEventBus(x =>
    {
        x.AddConsumer<PaymentCreatedConsumer, PaymentCreatedConsumerDefinition>();
        x.AddConsumer<LoanRepaymentRequestedConsumer>();
        x.AddConsumer<FraudEvaluatedConsumer>();
        // Note: UseBusOutbox() is intentionally omitted here. PaymentCreatedConsumer does
        // not publish any events, so the EF transactional outbox / InboxState idempotency
        // layer adds SaveChangesAsync overhead without providing reliability benefits.
        // Duplicate-message protection is handled at the domain level via the unique
        // (WalletId, TransactionId) index on LedgerEntry.
    });
}

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddScoped<IWalletService, WalletService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation("WalletService starting in environment: {Environment}", app.Environment.EnvironmentName);

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.MapGet("/", () => "Wallet Service is running.");
app.MapGet("/env", () => app.Environment.EnvironmentName);
app.MapGet("/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
    string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));

// Ensure database is created
await app.SafeMigrateAsync<WalletDbContext>();

app.Run();
