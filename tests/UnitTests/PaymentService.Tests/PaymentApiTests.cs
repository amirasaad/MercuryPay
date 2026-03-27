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
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;

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
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:paymentdb", "");

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

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        });
    }

    [Fact]
    public async Task CreatePayment_RequiresAuthentication()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new PaymentRequest(100.00m, "USD", "user_123", "merchant_456", null);

        // Act
        var response = await client.PostAsJsonAsync("/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var request = new PaymentRequest(100.00m, "USD", "user_123", "merchant_456", null);

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
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var request = new PaymentRequest(-10.00m, "USD", "user_123", "merchant_456", null);

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
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        var createRequest = new PaymentRequest(50.00m, "USD", "user_A", "user_B", null);
        var createResponse = await client.PostAsJsonAsync("/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        // Act
        var getResponse = await client.GetAsync($"/payments/{createdPayment!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(createdPayment.Id, payment.Id);
    }

    [Fact]
    public async Task GetPayment_ReturnsNotFound_WhenPaymentDoesNotExist()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Act
        var response = await client.GetAsync($"/payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorization) || string.IsNullOrWhiteSpace(authorization.ToString()))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

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

/// <summary>
/// Security tests asserting that the dev auth bypass is only active in the Development environment.
/// </summary>
public class DevAuthBypassSecurityTests
{
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task DevAuthBypass_IsDisabled_WhenEnvironmentIsNotDevelopment(string environment)
    {
        // Arrange – simulate a misconfiguration where DisableAuthValidation=true is set
        // in a non-Development environment. The app must fail fast.
        var mockPublish = new Mock<IPublishEndpoint>();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:paymentdb", "");
            builder.UseSetting("Identity:Authority", "https://dummy-auth.example.com");
            builder.UseSetting("Identity:Audience", "account");
            builder.UseSetting("Identity:DisableAuthValidation", "true");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPublishEndpoint));
                if (descriptor != null) services.Remove(descriptor);
                services.AddScoped(_ => mockPublish.Object);
            });
        });

        // Act + Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var client = factory.CreateClient();
            await client.GetAsync("/");
        });
        Assert.Contains("DisableAuthValidation", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
