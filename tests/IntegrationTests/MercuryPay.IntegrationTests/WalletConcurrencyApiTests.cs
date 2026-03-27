using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MercuryPay.IntegrationTests;

[Collection("DistributedApp")]
public class WalletConcurrencyApiTests
{
    [Fact]
    public async Task ConcurrentCredits_ShouldSucceed_AndResultInSummedBalance()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MercuryPay_AppHost>();
        appHost.Services.ConfigureHttpClientDefaults(client =>
        {
            client.AddStandardResilienceHandler();
            client.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();

        await app.StartAsync();

        await notifications.WaitForResourceAsync("postgres", KnownResourceStates.Running);
        await notifications.WaitForResourceAsync("messaging", KnownResourceStates.Running);
        await notifications.WaitForResourceAsync("walletservice", KnownResourceStates.Running);

        var walletClient = app.CreateHttpClient("walletservice", "https");
        walletClient.Timeout = TimeSpan.FromMinutes(5);

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

        var runId = Guid.NewGuid().ToString("N")[..8];
        var userId = $"concurrent_{runId}";
        var currency = "USD";

        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(userId));
        var createWalletResponse = await walletClient.PostAsJsonAsync("/Wallets", new { UserId = userId, Currency = currency });
        createWalletResponse.EnsureSuccessStatusCode();
        var wallet = await createWalletResponse.Content.ReadFromJsonAsync<WalletDto>();
        Assert.NotNull(wallet);

        async Task<HttpResponseMessage> CreditAsync(string transactionId, decimal amount)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/Wallets/{wallet.Id}/credit")
            {
                Content = JsonContent.Create(new { Amount = amount, TransactionId = transactionId, Description = "Concurrent credit" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(userId));
            return await walletClient.SendAsync(request);
        }

        var t1 = CreditAsync(Guid.NewGuid().ToString("N"), 10m);
        var t2 = CreditAsync(Guid.NewGuid().ToString("N"), 10m);
        var results = await Task.WhenAll(t1, t2);

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        walletClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateDevJwt(userId));
        var after = await walletClient.GetFromJsonAsync<WalletDto>($"/Wallets/{wallet.Id}");
        Assert.NotNull(after);
        Assert.Equal(20m, after.Balance);
    }

    private sealed record WalletDto(Guid Id, string UserId, string Currency, decimal Balance);
}

