using MassTransit;
using MercuryPay.RiskService.Consumers;
using MercuryPay.RiskService.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("riskdb");
if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("Ssl Mode") && !connectionString.Contains("SslMode"))
{
    builder.Configuration["ConnectionStrings:riskdb"] = $"{connectionString};Ssl Mode=Disable";
}

builder.AddNpgsqlDbContext<RiskDbContext>("riskdb");

builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(includeNamespace: true));

    x.AddConsumer<PaymentCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var messagingConnectionString = builder.Configuration.GetConnectionString("messaging");
        if (!string.IsNullOrEmpty(messagingConnectionString))
        {
            ConfigureRabbitMqHost(cfg, messagingConnectionString);
        }
        cfg.ConfigureEndpoints(context);
    });

    x.AddEntityFrameworkOutbox<RiskDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
        o.UseBusOutbox();
    });
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Apply migrations automatically
await app.SafeMigrateAsync<RiskDbContext>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

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
