using Microsoft.Playwright;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aspire.Hosting.ApplicationModel;
using System.Text.RegularExpressions;

namespace MercuryPay.E2E.Tests;

public class WalletTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MercuryPay_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
        { 
            Headless = true 
        });
        
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
    public async Task CreateWallet_ShouldAddWalletToList()
    {
        // 1. Wait for services
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("webfrontend", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("keycloak", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        // Wait for Keycloak readiness
        var keycloakClient = _app.CreateHttpClient("keycloak");
        await WaitForKeycloakAsync(keycloakClient);

        // 2. Login
        await LoginAsync("alice", "alice");

        // 3. Navigate to Wallets
        var httpClient = _app.CreateHttpClient("webfrontend");
        var baseAddress = httpClient.BaseAddress ?? throw new Exception("Could not determine base address");
        
        await _page.GotoAsync($"{baseAddress}wallets");
        
        // 4. Check if "Create USD Wallet" button exists
        // Wait for loading to finish
        await _page.WaitForSelectorAsync("h1:has-text('My Wallets')");
        
        // Check if we already have wallets (might be persistent from other tests or seed)
        // If "Create USD Wallet" button is present, click it.
        var createButton = _page.Locator("button:has-text('Create USD Wallet')");
        if (await createButton.IsVisibleAsync())
        {
            await createButton.ClickAsync();
        }

        // 5. Verify Wallet Card appears
        // Wait for card title "USD Wallet"
        await _page.WaitForSelectorAsync(".card-title:has-text('USD Wallet')", new PageWaitForSelectorOptions { Timeout = 10000 });
        
        var walletCard = _page.Locator(".card-title:has-text('USD Wallet')");
        var count = await walletCard.CountAsync();
        Assert.True(count > 0, "Wallet card should be visible");
    }

    private async Task LoginAsync(string username, string password)
    {
        var httpClient = _app.CreateHttpClient("webfrontend");
        var baseAddress = httpClient.BaseAddress ?? throw new Exception("Could not determine base address");

        await _page.GotoAsync(baseAddress.ToString());
        
        // Click Login
        var loginLink = _page.Locator("text=Log in").First;
        await loginLink.WaitForAsync();
        await loginLink.ClickAsync();

        // Wait for Keycloak
        await _page.WaitForURLAsync(new Regex(".*realms/mercury.*"), new PageWaitForURLOptions { Timeout = 120000 });
        
        // Fill Form
        await _page.WaitForSelectorAsync("#username");
        await _page.FillAsync("#username", username);
        await _page.FillAsync("#password", password);
        
        var submitButton = _page.Locator("input[type='submit'], button[type='submit'], #kc-login").First;
        await submitButton.ClickAsync();

        // Wait for redirect back to app (look for "Hello,")
        await _page.WaitForSelectorAsync("text=Hello,", new PageWaitForSelectorOptions { Timeout = 30000 });
    }

    private static async Task WaitForKeycloakAsync(HttpClient client)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(120))
        {
            try
            {
                var response = await client.GetAsync("/realms/mercury");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Ignore connection errors
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Keycloak did not start in time.");
    }
}
