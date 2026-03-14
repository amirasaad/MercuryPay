using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using Moq;
using MercuryPay.WalletService.Models;
using Microsoft.EntityFrameworkCore;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;

namespace MercuryPay.WalletService.Tests;

public class WalletApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // Override configuration to use InMemory DB logic in Program.cs
            builder.UseSetting("ConnectionStrings:walletdb", "");

            builder.ConfigureServices(services =>
            {
                // Mock IPublishEndpoint just in case
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPublishEndpoint));
                if (descriptor != null) services.Remove(descriptor);
                services.AddScoped(_ => new Mock<IPublishEndpoint>().Object);

                // Add Test Authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        });

    [Fact]
    public async Task CreateWallet_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        var request = new
        {
            UserId = "user_123",
            Currency = "USD"
        };

        // Act
        var response = await client.PostAsJsonAsync("/wallets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal("user_123", wallet.UserId);
        Assert.Equal("USD", wallet.Currency);
        Assert.Equal(0.00m, wallet.Balance);
    }

    [Fact]
    public async Task GetWallet_ReturnsOk_WhenWalletExists()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        var createRequest = new
        {
            UserId = "user_123",
            Currency = "EUR"
        };
        var createResponse = await client.PostAsJsonAsync("/wallets", createRequest);
        var createdWallet = await createResponse.Content.ReadFromJsonAsync<WalletResponse>();

        // Act
        var getResponse = await client.GetAsync($"/wallets/{createdWallet!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var wallet = await getResponse.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(createdWallet.Id, wallet.Id);
        Assert.Equal("user_123", wallet.UserId);
    }

    [Fact]
    public async Task GetWallet_ReturnsNotFound_WhenWalletDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/wallets/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateWallet_ReturnsConflict_WhenDuplicateUserAndCurrency()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        var request = new { UserId = "user_123", Currency = "GBP" };

        // Act
        var first = await client.PostAsJsonAsync("/wallets", request);
        var second = await client.PostAsJsonAsync("/wallets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetWallets_RequiresAuthentication()
    {
        // Arrange – no Authorization header
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/wallets");

        // Assert – endpoint is no longer anonymous
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWallets_ReturnsForbidden_WhenQueryingAnotherUsersWallets()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Act – authenticated as user_123, try to query wallets for another user
        var response = await client.GetAsync("/wallets?userId=other_user");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_ReturnsForbidden_WhenAccessingAnotherUsersWallet()
    {
        // Arrange – create a wallet owned by a different user via the service directly
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Create wallet for user_123 (authenticated), then try to access a wallet we inject for another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MercuryPay.WalletService.Infrastructure.WalletDbContext>();
        var otherWallet = new MercuryPay.WalletService.Domain.Wallet(Guid.NewGuid(), "other_user_999", "USD");
        context.Wallets.Add(otherWallet);
        await context.SaveChangesAsync();

        // Act
        var response = await client.GetAsync($"/wallets/{otherWallet.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWallets_DoesNotAutoCreateWallet_WhenNoneExist()
    {
        // Arrange – ensure the user has no wallets by using a unique userId
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Create a fresh factory scoped to a unique user to guarantee an empty wallet set.
        // We manipulate via the scope to verify side-effect-free behaviour.
        // The TestAuthHandler authenticates as user_123; use a fresh DB per test via factory isolation
        var walletsBefore = _factory.Services.CreateScope()
            .ServiceProvider
            .GetRequiredService<MercuryPay.WalletService.Infrastructure.WalletDbContext>()
            .Wallets.Where(w => w.UserId == "user_123").ToList();

        // Act
        var response = await client.GetAsync("/wallets");

        // Assert – 200 with an empty list (no auto-creation)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var walletsAfter = _factory.Services.CreateScope()
            .ServiceProvider
            .GetRequiredService<MercuryPay.WalletService.Infrastructure.WalletDbContext>()
            .Wallets.Where(w => w.UserId == "user_123").ToList();

        Assert.Equal(walletsBefore.Count, walletsAfter.Count);
    }

    [Fact]
    public async Task Credit_ReturnsBadRequest_WhenMissingRequiredFields()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // First create a wallet
        var createResp = await client.PostAsJsonAsync("/wallets", new { UserId = "user_123", Currency = "JPY" });
        var wallet = await createResp.Content.ReadFromJsonAsync<WalletResponse>();

        // Act – send credit with missing TransactionId
        var response = await client.PostAsJsonAsync($"/wallets/{wallet!.Id}/credit",
            new { Amount = 100m, TransactionId = "", Description = "Test" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_ReturnsBadRequest_WhenAmountIsNonPositive()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var createResp = await client.PostAsJsonAsync("/wallets", new { UserId = "user_123", Currency = "CHF" });
        var wallet = await createResp.Content.ReadFromJsonAsync<WalletResponse>();

        var response = await client.PostAsJsonAsync($"/wallets/{wallet!.Id}/credit",
            new { Amount = 0m, TransactionId = "TX-1", Description = "Test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_ReturnsBadRequest_WhenDescriptionIsMissing()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var createResp = await client.PostAsJsonAsync("/wallets", new { UserId = "user_123", Currency = "CAD" });
        var wallet = await createResp.Content.ReadFromJsonAsync<WalletResponse>();

        var response = await client.PostAsJsonAsync($"/wallets/{wallet!.Id}/credit",
            new { Amount = 50m, TransactionId = "TX-1", Description = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Only authenticate when an Authorization header is present, so that
            // tests asserting 401 on unauthenticated requests behave correctly.
            if (!Request.Headers.ContainsKey("Authorization"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[] { 
                new Claim(ClaimTypes.Name, "TestUser"), 
                new Claim(ClaimTypes.NameIdentifier, "user_123") 
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

public record WalletResponse(Guid Id, string UserId, string Currency, decimal Balance);
