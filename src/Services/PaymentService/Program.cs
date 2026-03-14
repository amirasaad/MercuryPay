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
    builder.AddEventBus(x =>
    {
        x.AddConsumer<LoanApprovedConsumer, LoanApprovedConsumerDefinition>();
        x.AddConsumer<FraudEvaluatedConsumer>();

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
