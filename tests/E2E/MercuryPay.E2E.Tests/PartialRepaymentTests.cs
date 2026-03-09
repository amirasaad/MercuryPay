using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace MercuryPay.E2E.Tests;

public class PartialRepaymentTests : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private readonly ITestOutputHelper _output;

    public PartialRepaymentTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
    public async Task PartialRepayment_ShouldUpdateInstallmentStatus()
    {
        // 1. Wait for services (Skip explicit WaitForResourceAsync as it times out on RabbitMQ in this env)
        _output.WriteLine("Starting test execution without explicit resource waits...");
        
        var lendingClient = _app.CreateHttpClient("lendingservice");
        lendingClient.Timeout = TimeSpan.FromMinutes(5);
        lendingClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");
        
        // 2. Create Loan (with retry for cold start)
        var userId = "test-user-partial-repay";
        var amount = 1200m;
        var currency = "USD";
        
        LoanDto? loan = null;
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromMinutes(2))
        {
            try
            {
                var response = await lendingClient.PostAsJsonAsync("/loans", new 
                { 
                    UserId = userId, 
                    Amount = amount, 
                    Currency = currency,
                    TermMonths = 12
                });
                
                if (response.IsSuccessStatusCode)
                {
                    loan = await response.Content.ReadFromJsonAsync<LoanDto>();
                    break;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _output.WriteLine($"Create Loan failed with {response.StatusCode}. Error: {error}. Retrying...");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Create Loan threw {ex.GetType().Name}: {ex.Message}. Retrying...");
            }
            await Task.Delay(2000);
        }

        Assert.NotNull(loan);
        _output.WriteLine($"Loan created: {loan.Id}");

        // 3. Wait for Approval (Disbursement)
        await WaitForLoanStatusAsync(lendingClient, loan.Id, "Approved");

        // 3b. Wait for Wallet Disbursement
        var walletClient = _app.CreateHttpClient("walletservice");
        walletClient.Timeout = TimeSpan.FromMinutes(5);
        walletClient.DefaultRequestHeaders.Authorization = lendingClient.DefaultRequestHeaders.Authorization;

        _output.WriteLine("Waiting for wallet disbursement...");
        bool fundsReceived = false;
        var disbursementWaitStart = DateTime.UtcNow;
        while (DateTime.UtcNow - disbursementWaitStart < TimeSpan.FromMinutes(2))
        {
            try
            {
                var walletsResponse = await walletClient.GetAsync("/wallets");
                if (walletsResponse.IsSuccessStatusCode)
                {
                    var wallets = await walletsResponse.Content.ReadFromJsonAsync<List<WalletDto>>();
                    var wallet = wallets?.FirstOrDefault(w => w.Currency == currency);
                    if (wallet != null && wallet.Balance >= amount)
                    {
                        _output.WriteLine($"Wallet funded: {wallet.Balance} {wallet.Currency}");
                        fundsReceived = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Check wallet failed: {ex.Message}");
            }
            await Task.Delay(2000);
        }
        Assert.True(fundsReceived, "Wallet was not funded with loan amount");

        // 4. Get Loan details with Schedule
        var loanDetails = await GetLoanDetailsAsync(lendingClient, loan.Id);
        Assert.NotNull(loanDetails);
        Assert.NotNull(loanDetails.RepaymentSchedule);
        
        var firstInstallment = loanDetails.RepaymentSchedule.Installments.OrderBy(i => i.DueDate).First();
        var installmentTotal = firstInstallment.TotalAmount;
        var partialAmount = Math.Round(installmentTotal / 2, 2);

        // 5. Make Partial Payment
        var repayResponse = await lendingClient.PostAsJsonAsync($"/loans/{loan.Id}/repay", new { Amount = partialAmount });
        repayResponse.EnsureSuccessStatusCode();

        // 6. Verify Partial Status
        // Wait briefly for processing if async, though currently LendingService updates synchronously before returning Accepted? 
        // Actually, it publishes an event but updates status to "RepaymentProcessing".
        // The consumer processes it and updates to "PartiallyPaid" or "Paid". We need to poll.
        
        await WaitForInstallmentStatusAsync(lendingClient, loan.Id, firstInstallment.DueDate, "PartiallyPaid", partialAmount);

        // 7. Make Remaining Payment
        var remainingAmount = installmentTotal - partialAmount;
        // Add a small buffer or check if exact amount works (it should)
        
        repayResponse = await lendingClient.PostAsJsonAsync($"/loans/{loan.Id}/repay", new { Amount = remainingAmount });
        repayResponse.EnsureSuccessStatusCode();

        // 8. Verify Paid Status
        await WaitForInstallmentStatusAsync(lendingClient, loan.Id, firstInstallment.DueDate, "Paid", installmentTotal);
    }

    private static async Task WaitForLoanStatusAsync(HttpClient client, Guid loanId, string expectedStatus)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(60))
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

    private static async Task<LoanDto?> GetLoanDetailsAsync(HttpClient client, Guid loanId)
    {
        var response = await client.GetAsync($"/loans/{loanId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoanDto>();
    }

    private static async Task WaitForInstallmentStatusAsync(HttpClient client, Guid loanId, DateTime dueDate, string expectedStatus, decimal expectedPaidAmount)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(60))
        {
            var loan = await GetLoanDetailsAsync(client, loanId);
            if (loan?.RepaymentSchedule != null)
            {
                var installment = loan.RepaymentSchedule.Installments.FirstOrDefault(i => i.DueDate == dueDate);
                if (installment != null)
                {
                    if (installment.Status == expectedStatus && Math.Abs(installment.PaidAmount - expectedPaidAmount) < 0.01m)
                    {
                        return;
                    }
                }
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Installment with due date {dueDate} did not reach status {expectedStatus} with paid amount {expectedPaidAmount} in time.");
    }

    // DTOs
    record LoanDto(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, RepaymentScheduleDto? RepaymentSchedule);
    record RepaymentScheduleDto(List<InstallmentDto> Installments, decimal TotalInterest, decimal AnnualInterestRate);
    record InstallmentDto(DateTime DueDate, decimal PrincipalAmount, decimal InterestAmount, decimal TotalAmount, decimal PaidAmount, string Status);
    public record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
}
