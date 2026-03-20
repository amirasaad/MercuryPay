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
            // Do NOT add AddStandardResilienceHandler() here. (See F-26 in docs/Findings-Backlog.md)
            // Its attempt-timeout (default 10 s) silently retries POST /Wallets when the service is
            // slow during startup.  If the server completed the first request before the timeout the
            // retry arrives at a wallet that already exists and returns 409, failing the test.
            // Retry logic is managed explicitly by PostWithRetriesAsync below.
            client.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        
        await app.StartAsync();

        // Wait for services and message broker to be ready
        await resourceNotificationService.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        var walletClient = app.CreateHttpClient("walletservice", "http");

        paymentClient.Timeout = TimeSpan.FromMinutes(5);
        walletClient.Timeout = TimeSpan.FromMinutes(5);
        
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

        var runId = Guid.NewGuid().ToString("N")[..8];
        var fromUserId = $"sender_{runId}";
        var toUserId = $"receiver_{runId}";
        var currency = "USD";
        var initialCredit = 1000m;
        var paymentAmount = 100m;

        // 1. Create Sender Wallet
        output.WriteLine("Creating Sender Wallet...");
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(fromUserId));
        var fromWallet = await EnsureWalletAsync(walletClient, currency, "Sender");

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
        var toWallet = await EnsureWalletAsync(walletClient, currency, "Receiver");

        // 4. Create Payment
        // Before creating the payment, ensure BOTH service buses are fully connected:
        // - WalletService: its consumer queue must be bound to the PaymentCreated exchange
        // - PaymentService: its bus must be connected so the outbox delivery job can send
        //   the PaymentCreated message to RabbitMQ (PaymentCreated goes through the EF
        //   transactional outbox; delivery requires an active bus connection)
        output.WriteLine("Waiting for WalletService MassTransit bus to be ready...");
        var walletHealthy = await WaitForServiceHealthyAsync(walletClient);
        output.WriteLine($"WalletService health check: {(walletHealthy ? "Healthy" : "Timed out — proceeding anyway")}");

        output.WriteLine("Waiting for PaymentService MassTransit bus to be ready...");
        var paymentHealthy = await WaitForServiceHealthyAsync(paymentClient);
        output.WriteLine($"PaymentService health check: {(paymentHealthy ? "Healthy" : "Timed out — proceeding anyway")}");

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
                var response = await client.PostAsJsonAsync(uri, body);
                // Return immediately on any 2xx (success) or 4xx (permanent client error — retrying won't help).
                // Only retry on 5xx server errors, which may be transient during service startup.
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500)
                {
                    lastResponse?.Dispose();
                    return response;
                }
                // 5xx: dispose the previous response before taking ownership of the new one.
                lastResponse?.Dispose();
                lastResponse = response;
            }
            catch (HttpRequestException)
            {
                // Network-level error (connection refused, DNS, etc.) — service may still be starting.
            }
            catch (TaskCanceledException)
            {
                // Request timeout during service startup — treat as transient and retry.
            }

            await Task.Delay(1000);
        }

        if (lastResponse != null)
        {
            return lastResponse;
        }

        throw new TimeoutException($"POST {uri} did not succeed within timeout.");
    }

    /// <summary>
    /// Creates a wallet for the currently-authenticated user, or returns the existing one if it was
    /// already created (e.g., by an infrastructure-level retry after a TCP connection drop).
    /// </summary>
    private async Task<WalletDto> EnsureWalletAsync(HttpClient walletClient, string currency, string label)
    {
        var createResponse = await PostWithRetriesAsync(walletClient, "/Wallets", new { Currency = currency });
        output.WriteLine($"Create {label} Wallet Response: {createResponse.StatusCode}");

        if (createResponse.IsSuccessStatusCode)
        {
            var wallet = await createResponse.Content.ReadFromJsonAsync<WalletDto>();
            Assert.NotNull(wallet);
            return wallet!;
        }

        if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            // The wallet was already created — most likely because a prior attempt succeeded on
            // the server but the TCP connection dropped before the client received the 201,
            // causing PostWithRetriesAsync to retry and produce a 409 on the second request.
            // Fetch the existing wallet via GET instead of failing the test.
            output.WriteLine($"{label} wallet already exists (409); fetching existing wallet.");
            var existing = await walletClient.GetFromJsonAsync<List<WalletDto>>("/Wallets");
            var wallet = existing?.FirstOrDefault(w => w.Currency == currency);
            Assert.NotNull(wallet);
            return wallet!;
        }

        var errorBody = await createResponse.Content.ReadAsStringAsync();
        output.WriteLine($"{label} wallet creation failed: {errorBody}");
        createResponse.EnsureSuccessStatusCode(); // always throws for non-success status codes
        throw new InvalidOperationException("Unreachable"); // satisfies the compiler
    }

    private async Task PollForBalanceAsync(HttpClient client, Guid walletId, decimal expectedBalance)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var start = DateTime.UtcNow;
        decimal? lastSeen = null;

        while (DateTime.UtcNow - start < timeout)
        {
            var wallet = await client.GetFromJsonAsync<WalletDto>($"/Wallets/{walletId}");
            if (wallet!.Balance == expectedBalance)
            {
                return;
            }
            lastSeen = wallet.Balance;
            await Task.Delay(1000);
        }

        throw new TimeoutException($"Wallet {walletId} balance did not reach {expectedBalance} within {timeout.TotalSeconds} seconds. Last observed balance: {lastSeen?.ToString() ?? "none (never polled successfully)"}.");
    }

    /// <summary>
    /// Polls the /health endpoint until it returns a 200 OK (all health checks pass, including
    /// the MassTransit bus health check), confirming that all consumer queues are bound.
    /// Returns true if healthy within the timeout, false otherwise.
    /// </summary>
    private static async Task<bool> WaitForServiceHealthyAsync(HttpClient client, int timeoutSeconds = 120)
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                // /health is AllowAnonymous and includes all health checks (including MassTransit bus)
                var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
                // Service not yet reachable — keep retrying
            }
            catch (TaskCanceledException)
            {
                // Request timed out — keep retrying
            }
            await Task.Delay(2000);
        }
        return false;
    }
}

public record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
public record PaymentRequest(string FromUserId, string ToUserId, decimal Amount, string Currency);
