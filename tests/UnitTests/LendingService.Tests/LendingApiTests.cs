using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MercuryPay.LendingService.Tests;

public class LendingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LendingApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateLoan_ReturnsCreated_WhenRequestIsValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            UserId = "user_123",
            Amount = 1000.00m,
            Currency = "USD"
        };

        // Act
        var response = await client.PostAsJsonAsync("/loans", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        Assert.Equal(request.UserId, loan.UserId);
        Assert.Equal(request.Amount, loan.Amount);
        Assert.Equal("Pending", loan.Status);
    }

    [Fact]
    public async Task CreateLoan_ReturnsBadRequest_WhenAmountIsNegative()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            UserId = "user_123",
            Amount = -500.00m,
            Currency = "USD"
        };

        // Act
        var response = await client.PostAsJsonAsync("/loans", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLoan_ReturnsOk_WhenLoanExists()
    {
        // Arrange
        var client = _factory.CreateClient();
        var createRequest = new
        {
            UserId = "user_123",
            Amount = 1000.00m,
            Currency = "USD"
        };
        var createResponse = await client.PostAsJsonAsync("/loans", createRequest);
        var createdLoan = await createResponse.Content.ReadFromJsonAsync<LoanResponse>();

        // Act
        var response = await client.GetAsync($"/loans/{createdLoan!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        Assert.Equal(createdLoan.Id, loan.Id);
    }

    [Fact]
    public async Task GetLoansByUser_ReturnsList_WhenUserHasLoans()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = $"user_{Guid.NewGuid()}";
        
        // Create 2 loans
        await client.PostAsJsonAsync("/loans", new { UserId = userId, Amount = 100.00m, Currency = "USD" });
        await client.PostAsJsonAsync("/loans", new { UserId = userId, Amount = 200.00m, Currency = "USD" });

        // Act
        var response = await client.GetAsync($"/loans/user/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loans = await response.Content.ReadFromJsonAsync<List<LoanResponse>>();
        Assert.NotNull(loans);
        Assert.Equal(2, loans.Count);
        Assert.All(loans, l => Assert.Equal(userId, l.UserId));
    }
}

public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status);
