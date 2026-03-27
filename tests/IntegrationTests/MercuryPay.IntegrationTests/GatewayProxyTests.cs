using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace MercuryPay.IntegrationTests;

[Collection("DistributedApp")]
public class GatewayProxyTests
{
    [Fact]
    public async Task Gateway_ShouldRejectAnonymousRequests_ToProtectedRoutes()
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

        await notifications.WaitForResourceHealthyAsync("keycloak");
        await notifications.WaitForResourceHealthyAsync("walletservice");
        await notifications.WaitForResourceHealthyAsync("apigateway");

        var gateway = app.CreateHttpClient("apigateway", "https");
        gateway.Timeout = TimeSpan.FromMinutes(2);

        var response = await gateway.GetAsync("/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_ShouldProxyRequests_WhenValidTokenProvided()
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

        await notifications.WaitForResourceHealthyAsync("keycloak");
        await notifications.WaitForResourceHealthyAsync("walletservice");
        await notifications.WaitForResourceHealthyAsync("apigateway");

        var keycloak = app.CreateHttpClient("keycloak", "http");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await WaitForKeycloakReadyAsync(keycloak, cts.Token);

        var tokenResponse = await keycloak.PostAsync(
            "/realms/mercury/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "web-app",
                ["username"] = "alice",
                ["password"] = "alice"
            })
        );
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        var gateway = app.CreateHttpClient("apigateway", "https");
        gateway.Timeout = TimeSpan.FromMinutes(2);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await gateway.GetAsync("/wallets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private static async Task WaitForKeycloakReadyAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync("/realms/mercury/.well-known/openid-configuration", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(1000, cancellationToken);
        }

        throw new TimeoutException("Keycloak did not become ready within the expected time.");
    }
}
