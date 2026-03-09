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
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<PaymentCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
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
