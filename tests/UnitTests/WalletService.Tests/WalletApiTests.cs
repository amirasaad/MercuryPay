using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MercuryPay.WalletService.Tests;

public class WalletApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task CreateWallet_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var client = _factory.CreateClient();
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
        Assert.Equal("user_456", wallet.UserId);
    }

    [Fact]
    public async Task GetWallet_ReturnsNotFound_WhenWalletDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/wallets/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public record WalletResponse(Guid Id, string UserId, string Currency, decimal Balance);
