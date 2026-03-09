using Microsoft.Playwright;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aspire.Hosting.ApplicationModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace MercuryPay.E2E.Tests;

public class LoanWorkflowTests : IAsyncLifetime
{
    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MercuryPay_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task LoanWorkflow_FullCycle_ShouldSucceed()
    {
        // 1. Wait for services
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("lendingservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("rabbitmq", KnownResourceStates.Running);

        var lendingClient = _app.CreateHttpClient("lendingservice");
        var walletClient = _app.CreateHttpClient("walletservice");

        // 2. Define User and Loan
        var userId = "test-user-workflow";
        var amount = 1000m;
        var currency = "USD";

        // 3. Create Loan
        // We bypass the UI for this API workflow test to ensure backend logic is correct
        var createLoanResponse = await lendingClient.PostAsJsonAsync("/loans", new 
        { 
            UserId = userId, 
            Amount = amount, 
            Currency = currency 
        });
        
        createLoanResponse.EnsureSuccessStatusCode();
        var loan = await createLoanResponse.Content.ReadFromJsonAsync<LoanDto>();
        Assert.NotNull(loan);
        Assert.Equal("Processing", loan.Status);

        // 4. Wait for Approval and Disbursement
        // This is async via MassTransit, so we need to poll
        await WaitForLoanStatusAsync(lendingClient, loan.Id, "Approved");
        
        // Check Wallet Balance (should be credited)
        // Need to find wallet first
        var wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, amount);
        Assert.NotNull(wallet);
        Assert.Equal(amount, wallet.Balance);

        // 5. Repay Loan
        var repayResponse = await lendingClient.PostAsync($"/loans/{loan.Id}/repay", null);
        repayResponse.EnsureSuccessStatusCode();

        // 6. Wait for Repayment Processing
        await WaitForLoanStatusAsync(lendingClient, loan.Id, "Repaid");

        // 7. Check Wallet Balance (should be debited back to 0)
        wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, 0);
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet.Balance);
    }

    private static async Task WaitForLoanStatusAsync(HttpClient client, Guid loanId, string expectedStatus)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(30))
        {
            var response = await client.GetAsync($"/loans/{loanId}");
            if (response.IsSuccessStatusCode)
            {
                var loan = await response.Content.ReadFromJsonAsync<LoanDto>();
                if (loan != null && loan.Status == expectedStatus)
                {
                    return;
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Loan {loanId} did not reach status {expectedStatus} in time.");
    }

    private async Task<WalletDto?> WaitForWalletBalanceAsync(HttpClient client, string userId, string currency, decimal expectedBalance)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(30))
        {
            var response = await client.GetAsync($"/wallets/user/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var wallets = await response.Content.ReadFromJsonAsync<List<WalletDto>>();
                var wallet = wallets?.FirstOrDefault(w => w.Currency == currency);
                if (wallet != null && wallet.Balance == expectedBalance)
                {
                    return wallet;
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Wallet for user {userId} did not reach balance {expectedBalance} in time.");
    }

    // DTOs for testing
    record LoanDto(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt);
    record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
}
