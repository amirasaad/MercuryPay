

using MercuryPay.WalletService.Services;
using MercuryPay.WalletService.Infrastructure;
using MercuryPay.WalletService.Consumers;
using Microsoft.EntityFrameworkCore;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddEventBus(x => 
{
    x.AddConsumer<PaymentCreatedConsumer>();
});

// Add services to the container.
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseInMemoryDatabase("WalletDb"));

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddScoped<IWalletService, WalletService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

public partial class Program { }
