using Microsoft.Playwright;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aspire.Hosting.ApplicationModel;

namespace MercuryPay.E2E.Tests;

public class HomePageTests : IAsyncLifetime
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
        
        // Create a new context that ignores HTTPS errors (common in dev/test)
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
    public async Task HomePage_ShouldLoad_AndShowWelcomeMessage()
    {
        // Get the URL for the webfrontend
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("webfrontend", KnownResourceStates.Running);

        var httpClient = _app.CreateHttpClient("webfrontend");
        var baseAddress = httpClient.BaseAddress ?? throw new Exception("Could not determine base address for webfrontend");

        // Navigate
        await _page.GotoAsync(baseAddress.ToString());

        // Assert Title
        var title = await _page.TitleAsync();
        Assert.Contains("Dashboard - MercuryPay", title);

        // Assert Welcome Message
        var welcomeMessage = await _page.Locator("h1").InnerTextAsync();
        Assert.Equal("Welcome to MercuryPay", welcomeMessage);
    }
}
