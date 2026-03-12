using MercuryPay.PaymentService.Services;
using MercuryPay.PaymentService.Infrastructure;
using MercuryPay.PaymentService.Consumers;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("paymentdb");
if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<PaymentDbContext>(options =>
        options.UseInMemoryDatabase("PaymentDb"));
}
else
{
    // Force disable SSL for local development with Aspire
    if (!connectionString.Contains("Ssl Mode") && !connectionString.Contains("SslMode"))
    {
        builder.Configuration["ConnectionStrings:paymentdb"] = $"{connectionString};Ssl Mode=Disable";
    }

    builder.AddNpgsqlDbContext<PaymentDbContext>("paymentdb", settings => 
        settings.DisableRetry = false); // Enable retry for resilience
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.AddEventBus((x) =>
    {
        x.AddConsumer<LoanApprovedConsumer>();
        x.AddConsumer<FraudEvaluatedConsumer>();
        x.AddConsumer<LoanInvalidatedConsumer>();
        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqConnectionString = builder.Configuration.GetConnectionString("messaging");
            if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
            {
                throw new InvalidOperationException("RabbitMQ connection is not configured.");
            }

            ConfigureRabbitMqHost(cfg, rabbitMqConnectionString);
            
            cfg.ReceiveEndpoint("loan-approved", e =>
            {
                e.ConfigureConsumer<LoanApprovedConsumer>(context);
                e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500)));
            });

            cfg.ConfigureEndpoints(context);
        });

        if (!string.IsNullOrEmpty(connectionString))
        {
            x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
                o.UseBusOutbox();
            });
        }
    });
}

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation("PaymentService starting in environment: {Environment}", app.Environment.EnvironmentName);

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

app.MapGet("/", () => "Payment Service is running.");
app.MapGet("/env", () => app.Environment.EnvironmentName);
app.MapGet("/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
    string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));

// Ensure database is created
await app.SafeMigrateAsync<PaymentDbContext>();

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
