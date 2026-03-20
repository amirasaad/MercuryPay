using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace MercuryPay.IntegrationTests;

[Collection("DistributedApp")]
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
            // Accept self-signed dev certs used by Aspire-launched services in the test environment.
            // Never use this handler outside of integration/test code.
            client.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        
        await app.StartAsync();

        // Wait for services to be ready
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        var walletClient = app.CreateHttpClient("walletservice", "http");

        paymentClient.Timeout = TimeSpan.FromMinutes(2);
        walletClient.Timeout = TimeSpan.FromMinutes(2);
        
        // Build simple dev JWTs that the Dev auth pipeline accepts in Development with validation disabled
        static string CreateDevJwt(string subject)
        {
            static string B64Url(string json)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                return Convert.ToBase64String(bytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }
            var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = B64Url($"{{\"sub\":\"{subject}\",\"name\":\"{subject}\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()} }}");
            return $"{header}.{payload}.";
        }

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

        var fromUserId = "user_sender";
        var toUserId = "user_receiver";
        var currency = "USD";
        var initialCredit = 1000m;
        var paymentAmount = 100m;

        // 1. Create Sender Wallet
        output.WriteLine("Creating Sender Wallet...");
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(fromUserId));
        var createFromWalletResponse = await PostWithRetriesAsync(walletClient, "/Wallets", new { Currency = currency });
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
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(fromUserId));
        var creditResponse = await PostWithRetriesAsync(walletClient, $"/Wallets/{fromWallet.Id}/credit",
            new { Amount = initialCredit, TransactionId = Guid.NewGuid().ToString(), Description = "Initial credit" });
        output.WriteLine($"Credit Response: {creditResponse.StatusCode}");
        creditResponse.EnsureSuccessStatusCode();

        // Verify credit
        var fromWalletAfterCredit = await walletClient.GetFromJsonAsync<WalletDto>($"/Wallets/{fromWallet.Id}");
        Assert.Equal(initialCredit, fromWalletAfterCredit!.Balance);

        // 3. Create Receiver Wallet
        output.WriteLine("Creating Receiver Wallet...");
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(toUserId));
        var createToWalletResponse = await PostWithRetriesAsync(walletClient, "/Wallets", new { Currency = currency });
        createToWalletResponse.EnsureSuccessStatusCode();
        var toWallet = await createToWalletResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(toWallet);

        // 4. Create Payment
        output.WriteLine("Creating Payment...");
        var paymentRequest = new PaymentRequest(fromUserId, toUserId, paymentAmount, currency);
        paymentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(fromUserId));
        var createPaymentResponse = await PostWithRetriesAsync(paymentClient, "/Payments", paymentRequest);
        output.WriteLine($"Create Payment Response: {createPaymentResponse.StatusCode}");
        if (!createPaymentResponse.IsSuccessStatusCode)
        {
             var error = await createPaymentResponse.Content.ReadAsStringAsync();
             output.WriteLine($"Payment Error: {error}");
        }
        createPaymentResponse.EnsureSuccessStatusCode();

        // 5. Poll for balance updates
        output.WriteLine("Polling for balance updates...");
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(fromUserId));
        await PollForBalanceAsync(walletClient, fromWallet.Id, initialCredit - paymentAmount);
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(toUserId));
        await PollForBalanceAsync(walletClient, toWallet.Id, paymentAmount);
    }

    private static async Task<HttpResponseMessage> PostWithRetriesAsync(HttpClient client, string uri, object body)
    {
        var timeout = TimeSpan.FromMinutes(6);
        var start = DateTime.UtcNow;
        HttpResponseMessage? lastResponse = null;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                lastResponse = await client.PostAsJsonAsync(uri, body);
                if (lastResponse.IsSuccessStatusCode)
                {
                    return lastResponse;
                }
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        if (lastResponse != null)
        {
            return lastResponse;
        }

        throw new TimeoutException($"POST {uri} did not succeed within timeout.");
    }

    private async Task PollForBalanceAsync(HttpClient client, Guid walletId, decimal expectedBalance)
    {
        var timeout = TimeSpan.FromMinutes(2); 
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
