using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MercuryPay.IntegrationTests;

[Collection("DistributedApp")]
public class LoanInvalidationFlowTests(ITestOutputHelper output)
{
    private const int EventProcessingDelayMs = 5000; // Increased delay to ensure message propagation

    [Fact]
    public async Task InvalidLoan_ShouldCancelPendingPayments()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPIRE_EPHEMERAL_POSTGRES", "true");
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
        var resourceNotifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync();

        // Wait for services
        await resourceNotifications.WaitForResourceAsync("keycloak", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("lendingservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        var lendingClient = app.CreateHttpClient("lendingservice", "https");

        var keycloakClient = app.CreateHttpClient("keycloak", "http");
        await WaitForKeycloakAsync(keycloakClient);
        var accessToken = await GetAccessTokenAsync(keycloakClient, username: "alice", password: "alice");
        lendingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act 1: Create an excessive loan (should be valid initially, then invalidated by cleanup)
        var loanRequest = new
        {
            userId = "user_alice",
            amount = 150000.00m, // > 100,000 limit
            currency = "USD",
            termMonths = 12
        };

        var loanResponse = await lendingClient.PostAsJsonAsync("/loans", loanRequest);
        if (!loanResponse.IsSuccessStatusCode)
        {
            var content = await loanResponse.Content.ReadAsStringAsync();
            output.WriteLine($"Loan creation failed: {loanResponse.StatusCode} - {content}");
            output.WriteLine("WWW-Authenticate: " + loanResponse.Headers.WwwAuthenticate);
        }
        loanResponse.EnsureSuccessStatusCode();
        var loan = await loanResponse.Content.ReadFromJsonAsync<LoanResponse>();
        Assert.NotNull(loan);
        output.WriteLine($"Created Loan: {loan.Id} Status: {loan.Status}");

        // Act 2: Create a payment referencing this loan
        var paymentRequest = new
        {
            fromUserId = "user_alice",
            toUserId = "user_bob",
            amount = 500.00m,
            currency = "USD",
            referenceId = loan.Id
        };

        var paymentResponse = await paymentClient.PostAsJsonAsync("/payments", paymentRequest);
        paymentResponse.EnsureSuccessStatusCode();
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("Pending", payment.Status);
        output.WriteLine($"Created Payment: {payment.Id} Status: {payment.Status}");

        // Act 3: Trigger Cleanup
        output.WriteLine("Triggering Cleanup...");
        var cleanupResponse = await lendingClient.PostAsync("/Cleanup/trigger", null);
        if (!cleanupResponse.IsSuccessStatusCode)
        {
            var content = await cleanupResponse.Content.ReadAsStringAsync();
            output.WriteLine($"Cleanup trigger failed: {cleanupResponse.StatusCode} - {content}");
        }
        cleanupResponse.EnsureSuccessStatusCode();

        // Wait for event processing (LoanInvalidated -> PaymentService Consumer)
        // Polling loop to wait for status change
        PaymentResponse? finalPayment = null;
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(2000);
            var getP = await paymentClient.GetAsync($"/payments/{payment.Id}");
            if (getP.IsSuccessStatusCode)
            {
                finalPayment = await getP.Content.ReadFromJsonAsync<PaymentResponse>();
                if (finalPayment?.Status == "Rejected")
                {
                    break;
                }
            }
        }

        // Assert: Verify Loan is Invalid
        LoanResponse? updatedLoan = null;
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1000);
            var getLoanResponse = await lendingClient.GetAsync($"/loans/{loan.Id}");
            if (getLoanResponse.IsSuccessStatusCode)
            {
                updatedLoan = await getLoanResponse.Content.ReadFromJsonAsync<LoanResponse>();
                if (updatedLoan?.Status == "Invalid")
                {
                    break;
                }
            }
        }
        Assert.NotNull(updatedLoan);
        Assert.Equal("Invalid", updatedLoan!.Status);
        output.WriteLine($"Updated Loan Status: {updatedLoan.Status}");

        // Assert: Verify Payment is Rejected
        Assert.NotNull(finalPayment);
        Assert.Equal("Rejected", finalPayment.Status);
        output.WriteLine($"Updated Payment Status: {finalPayment.Status}");
    }

    private static async Task WaitForKeycloakAsync(HttpClient client)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < timeout)
        {
            try
            {
                var response = await client.GetAsync("/realms/mercury");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Keycloak did not start in time.");
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient keycloakClient, string username, string password)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var start = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            try
            {
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/realms/mercury/protocol/openid-connect/token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "password",
                        ["client_id"] = "web-app",
                        ["scope"] = "openid",
                        ["username"] = username,
                        ["password"] = password
                    })
                };

                var response = await keycloakClient.SendAsync(tokenRequest);
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Keycloak token request failed: {(int)response.StatusCode} {response.StatusCode}. {content}");
                }
                var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
                if (payload != null)
                {
                    var bearer = SelectBearerToken(payload);
                    if (!string.IsNullOrWhiteSpace(bearer))
                    {
                        return bearer;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Unable to obtain access token from Keycloak within timeout.");
    }

    private static string SelectBearerToken(TokenResponse payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.AccessToken) && payload.AccessToken.Contains('.'))
        {
            return payload.AccessToken;
        }

        if (!string.IsNullOrWhiteSpace(payload.IdToken) && payload.IdToken.Contains('.'))
        {
            return payload.IdToken;
        }

        return string.Empty;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;
    }
}

public record LoanResponse(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt, int TermMonths, decimal AnnualInterestRate, object? RepaymentSchedule);
public record PaymentResponse(Guid Id, string Status, decimal Amount, string Currency, string FromUserId, string ToUserId, Guid? ReferenceId);
