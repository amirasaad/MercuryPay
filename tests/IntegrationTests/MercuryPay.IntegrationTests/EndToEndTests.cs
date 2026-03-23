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
            client.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        
        await app.StartAsync();

        // Wait for services to be ready
        await resourceNotificationService.WaitForResourceAsync("postgres", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotificationService.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "https");
        var walletClient = app.CreateHttpClient("walletservice", "https");

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
            var payload = B64Url($"{{\"sub\":\"{subject}\",\"name\":\"{subject}\",\"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier\":\"{subject}\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()} }}");
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
        var createFromWalletResponse = await PostWithRetriesAsync(walletClient, "/Wallets", new { UserId = fromUserId, Currency = currency });
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
        var creditResponse = await PostWithRetriesAsync(
            walletClient,
            $"/Wallets/{fromWallet.Id}/credit",
            new { Amount = initialCredit, TransactionId = Guid.NewGuid().ToString(), Description = "Initial credit" }
        );
        output.WriteLine($"Credit Response: {creditResponse.StatusCode}");
        creditResponse.EnsureSuccessStatusCode();

        // Verify credit
        var fromWalletAfterCredit = await walletClient.GetFromJsonAsync<WalletDto>($"/Wallets/{fromWallet.Id}");
        Assert.Equal(initialCredit, fromWalletAfterCredit!.Balance);

        // 3. Create Receiver Wallet
        output.WriteLine("Creating Receiver Wallet...");
        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(toUserId));
        var createToWalletResponse = await PostWithRetriesAsync(walletClient, "/Wallets", new { UserId = toUserId, Currency = currency });
        createToWalletResponse.EnsureSuccessStatusCode();
        var toWallet = await createToWalletResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(toWallet);

        // 4. Create Payment
        // Before creating the payment, ensure BOTH service buses are fully connected:
        // - WalletService: its consumer queue must be bound to the PaymentCreated exchange
        // - PaymentService: its MassTransit bus must be connected so it can directly publish
        //   the PaymentCreated event to RabbitMQ when the HTTP /Payments request is handled
        //   (no EF transactional outbox is used for PaymentCreated in the PaymentService path)
        output.WriteLine("Waiting for WalletService MassTransit bus to be ready...");
        var walletHealthy = await WaitForServiceHealthyAsync(walletClient);
        output.WriteLine($"WalletService health check: {(walletHealthy ? "Healthy" : "Timed out")}");
        Assert.True(walletHealthy, "WalletService MassTransit bus must be healthy before creating payments.");

        output.WriteLine("Waiting for PaymentService MassTransit bus to be ready...");
        var paymentHealthy = await WaitForServiceHealthyAsync(paymentClient);
        output.WriteLine($"PaymentService health check: {(paymentHealthy ? "Healthy" : "Timed out")}");
        Assert.True(paymentHealthy, "PaymentService MassTransit bus must be healthy before creating payments.");

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
        var createdPayment = await createPaymentResponse.Content.ReadFromJsonAsync<PaymentResponseDto>();
        Assert.NotNull(createdPayment);

        var finalizedPayment = await PollForPaymentStatusAsync(paymentClient, createdPayment.Id);
        output.WriteLine($"Payment Status: {finalizedPayment.Status}");
        if (finalizedPayment.Status != "Approved")
        {
            throw new InvalidOperationException($"Payment {finalizedPayment.Id} was not approved. Status={finalizedPayment.Status} Reason={finalizedPayment.RejectionReason}");
        }

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

    private static async Task PollForBalanceAsync(HttpClient client, Guid walletId, decimal expectedBalance)
    {
        var timeout = TimeSpan.FromMinutes(5);
        var timeout = TimeSpan.FromMinutes(5);
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

        throw new TimeoutException($"Wallet {walletId} balance did not reach {expectedBalance} within {timeout.TotalSeconds} seconds.");
    }

    private static async Task<PaymentResponseDto> PollForPaymentStatusAsync(HttpClient client, Guid paymentId)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            var payment = await client.GetFromJsonAsync<PaymentResponseDto>($"/Payments/{paymentId}");
            if (payment is not null && payment.Status != "Pending")
            {
                return payment;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Payment {paymentId} status did not finalize within {timeout.TotalSeconds} seconds.");
    }
}

public record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
public record PaymentRequest(string FromUserId, string ToUserId, decimal Amount, string Currency);
public record PaymentResponseDto(Guid Id, string Status, decimal Amount, string Currency, string FromUserId, string ToUserId, Guid? ReferenceId, string? RejectionReason = null);
