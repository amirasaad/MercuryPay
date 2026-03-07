using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using MassTransit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using MercuryPay.PaymentService.Models;
using Microsoft.Extensions.Hosting;
using MercuryPay.BuildingBlocks.Events;

namespace MercuryPay.PaymentService.Tests;

public class PaymentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;

    public PaymentApiTests(WebApplicationFactory<Program> factory)
    {
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing registration if any
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPublishEndpoint));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                // Add mock
                services.AddScoped(_ => _mockPublishEndpoint.Object);
            });
        });
    }

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

        // Verify event was published
        _mockPublishEndpoint.Verify(x => x.Publish(It.IsAny<PaymentCreated>(), It.IsAny<CancellationToken>()), Times.Once);
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
        Assert.Equal(50.00m, createdPayment.Amount); 
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
