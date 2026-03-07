using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MercuryPay.IntegrationTests;

public class EndToEndTests(ITestOutputHelper output)
{
    [Fact]
    public async Task PaymentCreation_ShouldUpdateWalletBalance()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MercuryPay_AppHost>();
        
        // Ensure resources are running
        appHost.Services.ConfigureHttpClientDefaults(client =>
        {
            client.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        
        await app.StartAsync();

        // Wait for services to be ready
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice");
        var walletClient = app.CreateHttpClient("walletservice");

        output.WriteLine($"PaymentService BaseAddress: {paymentClient.BaseAddress}");
        output.WriteLine($"WalletService BaseAddress: {walletClient.BaseAddress}");

        // Verify Environment and Connectivity
        try 
        {
            var walletEnv = await walletClient.GetStringAsync("/env");
            output.WriteLine($"Wallet Env: {walletEnv}");
            var walletRoot = await walletClient.GetStringAsync("/");
            output.WriteLine($"Wallet Root: {walletRoot}");
            var walletRoutes = await walletClient.GetStringAsync("/routes");
            output.WriteLine($"Wallet Routes: \n{walletRoutes}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Wallet Connectivity Check Failed: {ex.Message}");
        }

        try 
        {
            var paymentRoot = await paymentClient.GetStringAsync("/");
            output.WriteLine($"Payment Root: {paymentRoot}");
            var paymentRoutes = await paymentClient.GetStringAsync("/routes");
            output.WriteLine($"Payment Routes: \n{paymentRoutes}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Payment Connectivity Check Failed: {ex.Message}");
        }

        // Verify Health
        var walletHealth = await walletClient.GetAsync("/health");
        output.WriteLine($"Wallet Health: {walletHealth.StatusCode}");
        walletHealth.EnsureSuccessStatusCode();

        var paymentHealth = await paymentClient.GetAsync("/health");
        output.WriteLine($"Payment Health: {paymentHealth.StatusCode}");
        paymentHealth.EnsureSuccessStatusCode();

        var fromUserId = "user_sender";
        var toUserId = "user_receiver";
        var currency = "USD";
        var initialCredit = 1000m;
        var paymentAmount = 100m;

        // 1. Create Sender Wallet
        output.WriteLine("Creating Sender Wallet...");
        var createFromWalletResponse = await walletClient.PostAsJsonAsync("/Wallets", new { UserId = fromUserId, Currency = currency });
        output.WriteLine($"Create Sender Wallet Response: {createFromWalletResponse.StatusCode}");
        if (!createFromWalletResponse.IsSuccessStatusCode)
        {
            var error = await createFromWalletResponse.Content.ReadAsStringAsync();
            output.WriteLine($"Error: {error}");
        }
        createFromWalletResponse.EnsureSuccessStatusCode();
        var fromWallet = await createFromWalletResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(fromWallet);

        // 2. Credit Sender Wallet
        output.WriteLine("Crediting Sender Wallet...");
        var creditResponse = await walletClient.PostAsJsonAsync($"/Wallets/{fromWallet.Id}/credit", initialCredit);
        output.WriteLine($"Credit Response: {creditResponse.StatusCode}");
        creditResponse.EnsureSuccessStatusCode();

        // Verify credit
        var fromWalletAfterCredit = await walletClient.GetFromJsonAsync<WalletDto>($"/Wallets/{fromWallet.Id}");
        Assert.Equal(initialCredit, fromWalletAfterCredit!.Balance);

        // 3. Create Receiver Wallet
        output.WriteLine("Creating Receiver Wallet...");
        var createToWalletResponse = await walletClient.PostAsJsonAsync("/Wallets", new { UserId = toUserId, Currency = currency });
        createToWalletResponse.EnsureSuccessStatusCode();
        var toWallet = await createToWalletResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(toWallet);

        // 4. Create Payment
        output.WriteLine("Creating Payment...");
        var paymentRequest = new PaymentRequest(fromUserId, toUserId, paymentAmount, currency);
        var createPaymentResponse = await paymentClient.PostAsJsonAsync("/Payments", paymentRequest);
        output.WriteLine($"Create Payment Response: {createPaymentResponse.StatusCode}");
        if (!createPaymentResponse.IsSuccessStatusCode)
        {
             var error = await createPaymentResponse.Content.ReadAsStringAsync();
             output.WriteLine($"Payment Error: {error}");
        }
        createPaymentResponse.EnsureSuccessStatusCode();

        // 5. Poll for balance updates
        output.WriteLine("Polling for balance updates...");
        await PollForBalanceAsync(walletClient, fromWallet.Id, initialCredit - paymentAmount);
        await PollForBalanceAsync(walletClient, toWallet.Id, paymentAmount);
    }

    private async Task PollForBalanceAsync(HttpClient client, Guid walletId, decimal expectedBalance)
    {
        var timeout = TimeSpan.FromSeconds(30); 
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            var wallet = await client.GetFromJsonAsync<WalletDto>($"/Wallets/{walletId}");
            if (wallet!.Balance == expectedBalance)
            {
                return;
            }
            await Task.Delay(1000);
        }

        throw new TimeoutException($"Wallet {walletId} balance did not reach {expectedBalance} within {timeout.TotalSeconds} seconds.");
    }
}

public record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
public record PaymentRequest(string FromUserId, string ToUserId, decimal Amount, string Currency);
