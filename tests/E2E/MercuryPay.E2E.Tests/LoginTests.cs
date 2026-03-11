using Microsoft.Playwright;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aspire.Hosting.ApplicationModel;

namespace MercuryPay.E2E.Tests;

public partial class LoginTests : IAsyncLifetime
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
    [Trait("Category", "E2E")]
    public async Task Login_WithValidCredentials_ShouldRedirectToHomeAndShowUserName()
    {
        // 1. Get Web Frontend URL
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("webfrontend", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("keycloak", KnownResourceStates.Running);

        // Wait for Keycloak to be ready (health check)
        var keycloakClient = _app.CreateHttpClient("keycloak");
        await WaitForKeycloakAsync(keycloakClient);

        var httpClient = _app.CreateHttpClient("webfrontend");
        var baseAddress = httpClient.BaseAddress ?? throw new Exception("Could not determine base address for webfrontend");

        // 2. Navigate to Home
        await _page.GotoAsync(baseAddress.ToString(), new PageGotoOptions { Timeout = 60000 });

        // 3. Click Login Link
        // Wait for the login link to be visible first to ensure page load
        // Use .First to avoid strict mode violation if multiple "Log in" links exist (e.g. in header and body)
        var loginLink = _page.Locator("text=Log in").First;
        await loginLink.WaitForAsync();
        
        var href = await loginLink.GetAttributeAsync("href");
        
        // Navigate directly to the href to ensure we trigger the challenge
        if (string.IsNullOrEmpty(href))
        {
            throw new Exception("Login link has no href");
        }
        
        var loginUrl = href.StartsWith("http") ? href : $"{baseAddress.ToString().TrimEnd('/')}/{href.TrimStart('/')}";
        
        await _page.GotoAsync(loginUrl);
  
        // 4. Wait for Keycloak Login Page
        // Wait for URL to contain "realms/mercury" which confirms redirection to Keycloak
        // Increase timeout to 60s for cold start
        await _page.WaitForURLAsync(MyRegex(), new PageWaitForURLOptions { Timeout = 60000 });
        
        // Wait for the form to appear
        await _page.WaitForSelectorAsync("#username");

        // 5. Fill Credentials (Alice)
        await _page.FillAsync("#username", "alice");
        await _page.FillAsync("#password", "alice");

        // 6. Submit Login
        // Try multiple common selectors for Keycloak login button
        var submitButton = _page.Locator("input[type='submit'], button[type='submit'], #kc-login").First;
        await submitButton.WaitForAsync();
        await submitButton.ClickAsync(); 

        // 7. Wait for Redirect Back to App
        // We might be redirected to HTTPS, so the port/protocol might change. 
        // Instead of waiting for a specific URL, we wait for the "Hello," text which confirms we are back and logged in.
        
        // 8. Assert User Name is Displayed
        // Check for "Hello, " text which appears only when logged in
        await _page.WaitForSelectorAsync("text=Hello,", new PageWaitForSelectorOptions { Timeout = 30000 });
        var welcomeText = await _page.Locator("text=Hello,").InnerTextAsync();
        Assert.Contains("Hello,", welcomeText); 
        
        // 9. Verify Dashboard Access (Wallets link should be visible/clickable)
        // Use First to avoid strict mode violation if there are multiple links (e.g. sidebar and dashboard cards)
        var walletsLink = await _page.Locator("a[href='wallets']").First.IsVisibleAsync();
        Assert.True(walletsLink, "Wallets link should be visible after login");
    }

    private async Task WaitForKeycloakAsync(HttpClient client)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(180))
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

    [System.Text.RegularExpressions.GeneratedRegex(".*realms/mercury.*")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
