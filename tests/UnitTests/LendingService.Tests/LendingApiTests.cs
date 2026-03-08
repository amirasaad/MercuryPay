using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;

namespace MercuryPay.LendingService.Tests;

public class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user_123") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class LendingApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
                
                // Ensure MassTransit uses InMemory for tests
                services.AddMassTransitTestHarness();
            });
        });

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
        Assert.Equal("Processing", loan.Status);
        Assert.Equal(12, loan.TermMonths); // Default
        Assert.Equal(0.05m, loan.AnnualInterestRate); // Default
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
        var userId = "user_123"; // Matches TestAuthHandler user
        
        // Create 2 loans
        await client.PostAsJsonAsync("/loans", new { UserId = userId, Amount = 100.00m, Currency = "USD" });
        await client.PostAsJsonAsync("/loans", new { UserId = userId, Amount = 200.00m, Currency = "USD" });

        // Act
        var response = await client.GetAsync($"/loans/user/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loans = await response.Content.ReadFromJsonAsync<List<LoanResponse>>();
        Assert.NotNull(loans);
        Assert.True(loans.Count >= 2); // Might have loans from other tests if using shared db
        Assert.All(loans, l => Assert.Equal(userId, l.UserId));
    }
    [Fact]
    public async Task RepayLoan_ReturnsAccepted_WhenLoanExists()
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

        // Manually approve the loan in the database to simulate Risk Service approval
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
            var loan = await context.Loans.FindAsync(createdLoan!.Id);
            if (loan != null)
            {
                loan.Approve();
                await context.SaveChangesAsync();
            }
        }

        // Act
        // Repay the first installment amount (approx 85.61)
        var repayRequest = new { Amount = 85.61m };
        var response = await client.PostAsJsonAsync($"/loans/{createdLoan!.Id}/repay", repayRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}

public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, int TermMonths = 12, decimal AnnualInterestRate = 0.05m);
