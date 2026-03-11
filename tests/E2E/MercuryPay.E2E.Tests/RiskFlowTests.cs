using Microsoft.Playwright;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aspire.Hosting.ApplicationModel;
using System.Net.Http.Json;

namespace MercuryPay.E2E.Tests;

public partial class RiskFlowTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        // Start Aspire App
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MercuryPay_AppHost>();
        
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
        { 
            Headless = true 
        });
        
        // Create a new context that ignores HTTPS errors
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        
        _page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        
        _playwright?.Dispose();
        
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task HighValuePayment_ShouldBeRejected_ByRiskService()
    {
        // 1. Wait for Services
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("riskservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await Task.Delay(2000);

        // 2. Create HTTP Client for Payment Service
        var httpClient = _app.CreateHttpClient("paymentservice");
        
        // 3. Initiate High Value Payment (> 10,000)
        var response = await httpClient.PostAsJsonAsync("/payments", new 
        {
            FromUserId = "alice",
            ToUserId = "bob",
            Amount = 15000,
            Currency = "USD"
        });

        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("Pending", payment.Status);

        // 4. Wait for Risk Evaluation (Async Process)
        // Poll status until it changes from Pending
        var finalStatus = await WaitForPaymentStatusAsync(httpClient, payment.Id, "Rejected");
        
        Assert.Contains("Rejected", finalStatus);
    }

    [Fact]
    public async Task LowValuePayment_ShouldBeApproved_ByRiskService()
    {
        // 1. Wait for Services
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("riskservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await Task.Delay(2000);

        // 2. Create HTTP Client for Payment Service
        var httpClient = _app.CreateHttpClient("paymentservice");
        
        // 3. Initiate Low Value Payment (< 10,000)
        var response = await httpClient.PostAsJsonAsync("/payments", new 
        {
            FromUserId = "alice",
            ToUserId = "bob",
            Amount = 500,
            Currency = "USD"
        });

        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("Pending", payment.Status);

        // 4. Wait for Risk Evaluation (Async Process)
        var finalStatus = await WaitForPaymentStatusAsync(httpClient, payment.Id, "Approved");
        
        Assert.Equal("Approved", finalStatus);
    }

    private async Task<string> WaitForPaymentStatusAsync(HttpClient client, Guid paymentId, string expectedStatusSubstring)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(120))
        {
            var response = await client.GetFromJsonAsync<PaymentResponse>($"/payments/{paymentId}");
            if (response != null && response.Status != "Pending")
            {
                return response.Status;
            }
            await Task.Delay(1000);
        }
        return "Pending (Timeout)";
    }

    // Helper Record for JSON Deserialization
    private record PaymentResponse(Guid Id, string Status, decimal Amount, string Currency, string FromUserId, string ToUserId);
}
