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
    builder.AddNpgsqlDbContext<WalletDbContext>("walletdb", settings => 
        settings.DisableRetry = true); // Disable retry as Outbox handles it
}

// Add Event Bus with Outbox configuration
builder.AddEventBus(x => 
{
    // x.SetKebabCaseEndpointNameFormatter(); // Already set in AddEventBus
    x.AddConsumer<PaymentCreatedConsumer>();
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        x.AddEntityFrameworkOutbox<WalletDbContext>(o =>
        {
            o.QueryDelay = TimeSpan.FromSeconds(1);
            o.UsePostgres();
            o.UseBusOutbox();
        });
    }

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddScoped<IWalletService, WalletService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation("WalletService starting in environment: {Environment}", app.Environment.EnvironmentName);

app.UseHttpsRedirection();

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

app.MapDefaultEndpoints();

app.Run();


