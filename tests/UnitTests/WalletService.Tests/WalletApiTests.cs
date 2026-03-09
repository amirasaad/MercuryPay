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

namespace MercuryPay.WalletService.Tests;

public class WalletApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
        {
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
            UserId = "user_456",
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
        // Note: The controller returns wallets for the authenticated user ("user_123")
        // But here we created a wallet for "user_456".
        // The controller Create method: 
        // var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // var wallet = _walletService.CreateWallet(userId ?? request.UserId, request.Currency);
        // If User is present ("user_123"), it overrides request.UserId!
        // So the wallet created will belong to "user_123".
        // And GetWallets returns wallets for "user_123".
        // But Get(id) just gets by ID.
        
        // Wait, let's check controller logic again.
        // Create: var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // So created wallet will be for "user_123".
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

    public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
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
