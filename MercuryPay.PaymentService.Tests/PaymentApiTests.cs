using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MercuryPay.PaymentService.Tests;

public class PaymentApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task CreatePayment_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            Amount = 100.00m,
            Currency = "USD",
            FromUserId = "user_123",
            ToUserId = "merchant_456"
        };

        // Act
        var response = await client.PostAsJsonAsync("/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var responseBody = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(responseBody);
        Assert.NotEqual(Guid.Empty, responseBody.Id);
        Assert.Equal("Pending", responseBody.Status);
    }
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
}
