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

// Add Event Bus with Outbox configuration
builder.AddEventBus(x => 
{
    // x.SetKebabCaseEndpointNameFormatter(); // Already set in AddEventBus
    x.AddConsumer<PaymentCreatedConsumer>();
    // LoanApprovedConsumer removed in favor of PaymentService flow
    x.AddConsumer<LoanRepaymentRequestedConsumer>();
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        x.AddEntityFrameworkOutbox<WalletDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres();
            o.UseBusOutbox();
        });
    }

    var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
    if (!string.IsNullOrEmpty(messagingConnectionString))
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            ConfigureRabbitMqHost(cfg, messagingConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    }
});

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

static void ConfigureRabbitMqHost(IRabbitMqBusFactoryConfigurator cfg, string connectionString)
{
    if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
    {
        var vhost = uri.AbsolutePath.Trim('/');
        var host = uri.Host;
        var port = (ushort)(uri.IsDefaultPort ? 5672 : uri.Port);

        cfg.Host(host, port, string.IsNullOrWhiteSpace(vhost) ? "/" : vhost, h =>
        {
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    h.Username(Uri.UnescapeDataString(parts[0]));
                }

                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    h.Password(Uri.UnescapeDataString(parts[1]));
                }
            }
        });

        return;
    }

    cfg.Host(connectionString);
}
