using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Consumers;
using MercuryPay.BuildingBlocks.Events;

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
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Remove existing DbContext options
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LendingDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add InMemory DbContext with isolated provider
                var efServiceProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.AddDbContext<LendingDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                    options.UseInternalServiceProvider(efServiceProvider);
                    options.ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
                
                // Ensure MassTransit uses InMemory for tests and registers consumers
                services.AddMassTransitTestHarness(x =>
                {
                    x.AddConsumer<LoanCreatedConsumer>();
                    x.AddConsumer<LoanRepaymentProcessedConsumer>();
                    x.AddConsumer<LoanApprovedFaultConsumer>();
                });
            });
        });

    [Fact]
    public async Task RepayLoan_PartiallyUpdatesInstallmentStatus()
    {
        // Arrange
        var client = _factory.CreateClient();
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        
        var createRequest = new { UserId = "user_123", Amount = 1000.00m, Currency = "USD" };
        var createResponse = await client.PostAsJsonAsync("/loans", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var loan = await createResponse.Content.ReadFromJsonAsync<LoanResponse>();
        var loanId = loan!.Id;

        // Wait for Loan to be Approved (consumed by LoanCreatedConsumer) with retry
        var consumed = false;
        for (int i = 0; i < 30; i++)
        {
            if (await harness.Consumed.Any<LoanCreated>())
            {
                consumed = true;
                break;
            }
            await Task.Delay(200);
        }
        Assert.True(consumed, "LoanCreated event was not consumed");

        // Verify status is Approved via API with retry
        LoanResponse? approvedLoan = null;
        for (int i = 0; i < 20; i++)
        {
            var response = await client.GetAsync($"/loans/{loanId}");
            var l = await response.Content.ReadFromJsonAsync<LoanResponse>();
            if (l!.Status == "Approved")
            {
                approvedLoan = l;
                break;
            }
            await Task.Delay(100);
        }
        Assert.NotNull(approvedLoan);
        Assert.Equal("Approved", approvedLoan.Status);

        // Act - Repay Partial Amount
        var firstInstallment = approvedLoan.RepaymentSchedule!.Installments.First();
        var partialAmount = firstInstallment.TotalAmount / 2;

        var repayResponse = await client.PostAsJsonAsync($"/loans/{loanId}/repay", new { Amount = partialAmount });
        Assert.Equal(HttpStatusCode.Accepted, repayResponse.StatusCode);

        // Verify LoanRepaymentRequested is published with retry
        var published = false;
        for (int i = 0; i < 30; i++)
        {
            if (await harness.Published.Any<LoanRepaymentRequested>())
            {
                published = true;
                break;
            }
            await Task.Delay(200);
        }
        Assert.True(published, "LoanRepaymentRequested event was not published");

        // Simulate LoanRepaymentProcessed (Success) from external service
        await harness.Bus.Publish(new LoanRepaymentProcessed(
            loanId,
            "user_123",
            partialAmount,
            true, // Success
            string.Empty,
            DateTime.UtcNow
        ));

        // Wait for LoanRepaymentProcessed to be consumed with retry
        var repaymentConsumed = false;
        for (int i = 0; i < 30; i++)
        {
            if (await harness.Consumed.Any<LoanRepaymentProcessed>())
            {
                repaymentConsumed = true;
                break;
            }
            await Task.Delay(200);
        }
        Assert.True(repaymentConsumed, "LoanRepaymentProcessed event was not consumed");

        // Verify Installment Status via API
        LoanResponse? updatedLoan = null;
        for (int i = 0; i < 20; i++)
        {
            var response = await client.GetAsync($"/loans/{loanId}");
            var l = await response.Content.ReadFromJsonAsync<LoanResponse>();
            
            var inst = l!.RepaymentSchedule!.Installments.First();
            
            // Wait for status update or amount update
            if (inst.PaidAmount > 0)
            {
                updatedLoan = l;
                break;
            }
            await Task.Delay(100);
        }

        Assert.NotNull(updatedLoan);
        var updatedInstallment = updatedLoan.RepaymentSchedule!.Installments.First();
        Assert.Equal("PartiallyPaid", updatedInstallment.Status);
        Assert.Equal(partialAmount, updatedInstallment.PaidAmount);
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
    public async Task CreateLoan_ReturnsBadRequest_WhenAmountExceedsMaximum()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            UserId = "user_123",
            Amount = 100001.00m,
            Currency = "USD"
        };

        var response = await client.PostAsJsonAsync("/loans", request);

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

public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, int TermMonths, decimal AnnualInterestRate, RepaymentScheduleDto? RepaymentSchedule = null);
public record InstallmentDto(DateTime DueDate, decimal PrincipalAmount, decimal InterestAmount, decimal TotalAmount, decimal PaidAmount, string Status);
public record RepaymentScheduleDto(List<InstallmentDto> Installments, decimal TotalInterest, decimal AnnualInterestRate);
