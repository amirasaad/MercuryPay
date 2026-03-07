using MercuryPay.LendingService.Controllers;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Services;
using MercuryPay.LendingService.Consumers;
using Microsoft.EntityFrameworkCore;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Event Bus
builder.AddEventBus(x => 
{
    x.AddConsumer<LoanApprovedFaultConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
        cfg.ConfigureEndpoints(context);
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add EF Core
builder.Services.AddDbContext<LendingDbContext>(options =>
    options.UseInMemoryDatabase("LendingDb"));

// Add Services
builder.Services.AddScoped<ILendingService, LendingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
