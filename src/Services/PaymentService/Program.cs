using MercuryPay.PaymentService.Services;
using MercuryPay.PaymentService.Infrastructure;
using MercuryPay.PaymentService.Consumers;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Authentication
builder.AddDefaultAuthentication();

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("paymentdb");
if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<PaymentDbContext>(options =>
        options.UseInMemoryDatabase("PaymentDb"));
}
else
{
    builder.AddNpgsqlDbContext<PaymentDbContext>("paymentdb", settings => 
        settings.DisableRetry = true); // Disable retry as Outbox handles it
}

// Add Event Bus with Outbox configuration
builder.AddEventBus((x) =>
{
    // x.SetKebabCaseEndpointNameFormatter(); // Already set in AddEventBus extension
    
    x.AddConsumer<LoanApprovedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("messaging"));
        
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

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation("PaymentService starting in environment: {Environment}", app.Environment.EnvironmentName);

app.UseHttpsRedirection();

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

app.MapDefaultEndpoints();

app.Run();
