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

    [Fact]
    public async Task CreatePayment_ReturnsBadRequest_WhenAmountIsNegative()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            Amount = -10.00m,
            Currency = "USD",
            FromUserId = "user_123",
            ToUserId = "merchant_456"
        };

        // Act
        var response = await client.PostAsJsonAsync("/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_ReturnsOk_WhenPaymentExists()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            Amount = 50.00m,
            Currency = "USD",
            FromUserId = "user_A",
            ToUserId = "user_B"
        };
        var createResponse = await client.PostAsJsonAsync("/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act
        var getResponse = await client.GetAsync($"/payments/{createdPayment!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(createdPayment.Id, payment.Id);
        Assert.Equal(50.00m, createdPayment.Amount); // Check if amount is returned correctly (assuming we add Amount to response)
    }

    [Fact]
    public async Task GetPayment_ReturnsNotFound_WhenPaymentDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
}
