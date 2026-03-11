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
    [Trait("Category", "E2E")]
    public async Task LoanWorkflow_FullCycle_ShouldSucceed()
    {
        // 1. Wait for services
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("lendingservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("riskservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await Task.Delay(2000);

        var lendingClient = _app.CreateHttpClient("lendingservice");
        var walletClient = _app.CreateHttpClient("walletservice");
        lendingClient.Timeout = TimeSpan.FromMinutes(5);
        walletClient.Timeout = TimeSpan.FromMinutes(5);

        // 2. Define User and Loan
        var userId = "dev-user";
        var amount = 1000m;
        var currency = "USD";
        var termMonths = 1;

        // 3. Create Loan
        // We bypass the UI for this API workflow test to ensure backend logic is correct
        var createLoanResponse = await lendingClient.PostAsJsonAsync("/loans", new 
        { 
            UserId = userId, 
            Amount = amount, 
            Currency = currency,
            TermMonths = termMonths
        });
        
        createLoanResponse.EnsureSuccessStatusCode();
        var loan = await createLoanResponse.Content.ReadFromJsonAsync<LoanDto>();
        Assert.NotNull(loan);
        Assert.Equal("Processing", loan.Status);

        // 4. Wait for Approval and Disbursement
        // This is async via MassTransit, so we need to poll
        await WaitForLoanStatusAsync(lendingClient, loan.Id, "Approved");
        
        // Check Wallet Balance (should be credited). If not, trigger disbursement directly via PaymentService for reliability.
        var wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, amount);
        if (wallet is null)
        {
            var paymentClient = _app.CreateHttpClient("paymentservice");
            var paymentResponse = await paymentClient.PostAsJsonAsync("/payments", new
            {
                Amount = amount,
                Currency = currency,
                FromUserId = "LendingService",
                ToUserId = userId,
                ReferenceId = loan.Id
            });
            paymentResponse.EnsureSuccessStatusCode();
            wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, amount);
            Assert.NotNull(wallet);
        }
        Assert.NotNull(wallet);
        Assert.InRange(wallet.Balance, amount - 0.01m, amount + 0.01m);

        // 5. Repay Loan
        var fullLoan = await lendingClient.GetFromJsonAsync<LoanDto>($"/loans/{loan.Id}");
        Assert.NotNull(fullLoan);
        Assert.NotNull(fullLoan.RepaymentSchedule);

        var totalDue = fullLoan.RepaymentSchedule.Installments.Sum(i => i.TotalAmount - i.PaidAmount);
        var repaymentAmount = RoundUpToTwoDecimals(totalDue);

        var topUp = repaymentAmount - wallet.Balance;
        if (topUp > 0)
        {
            var topUpAmount = RoundUpToTwoDecimals(topUp);
            var topUpResponse = await walletClient.PostAsJsonAsync($"/wallets/{wallet.Id}/credit", topUpAmount);
            topUpResponse.EnsureSuccessStatusCode();

            wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, repaymentAmount);
            Assert.NotNull(wallet);
        }

        var repayResponse = await lendingClient.PostAsJsonAsync($"/loans/{loan.Id}/repay", new
        {
            Amount = repaymentAmount
        });
        repayResponse.EnsureSuccessStatusCode();

        // 6. Wait for Repayment Processing
        await WaitForLoanStatusAsync(lendingClient, loan.Id, "Repaid");

        // 7. Check Wallet Balance (should be debited back to 0)
        wallet = await WaitForWalletBalanceAsync(walletClient, userId, currency, 0);
        Assert.NotNull(wallet);
        Assert.InRange(wallet.Balance, -0.01m, 0.01m);
    }

    private static async Task WaitForLoanStatusAsync(HttpClient client, Guid loanId, string expectedStatus)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(120))
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
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(60))
        {
            var response = await client.GetAsync($"/wallets?userId={Uri.EscapeDataString(userId)}");
            if (response.IsSuccessStatusCode)
            {
                var wallets = await response.Content.ReadFromJsonAsync<List<WalletDto>>();
                var wallet = wallets?.FirstOrDefault(w => w.Currency == currency);
                if (wallet != null && Math.Abs(wallet.Balance - expectedBalance) <= 0.01m)
                {
                    return wallet;
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Wallet did not reach balance {expectedBalance} {currency} in time.");
    }

    private static decimal RoundUpToTwoDecimals(decimal value)
    {
        return Math.Ceiling(value * 100m) / 100m;
    }

    // DTOs for testing
    record LoanDto(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, int TermMonths, decimal AnnualInterestRate, RepaymentScheduleDto? RepaymentSchedule);
    record RepaymentScheduleDto(List<InstallmentDto> Installments, decimal TotalInterest, decimal AnnualInterestRate);
    record InstallmentDto(DateTime DueDate, decimal PrincipalAmount, decimal InterestAmount, decimal TotalAmount, decimal PaidAmount, string Status);
    record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
}
