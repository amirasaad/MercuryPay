using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Xunit;
using Xunit.Abstractions;

namespace MercuryPay.IntegrationTests;

/// <summary>
/// Integration tests for Risk Service event flow.
/// Tests the full cycle: PaymentCreated → Risk Evaluation → FraudEvaluated
/// </summary>
[Collection("DistributedApp")]
public class RiskServiceEventFlowTests(ITestOutputHelper output)
{
    private const int EventProcessingDelayMs = 2000;
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 500;

    [Fact]
    public async Task EventFlow_SmallPayment_ShouldBeApprovedAndPublishEvent()
    {
        // Arrange
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
        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("riskservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        paymentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        // Act: Create a low-value payment (should be approved)
        var newPaymentRequest = new
        {
            fromUserId = "user_alice",
            toUserId = "user_bob",
            amount = 500.00m,
            currency = "USD"
        };

        var createResponse = await paymentClient.PostAsJsonAsync("/Payments", newPaymentRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(createdPayment);
        Assert.NotEqual(Guid.Empty, createdPayment.Id);

        output.WriteLine($"Created Payment: {createdPayment.Id}");

        // Assert: Verify payment is in Approved state after risk evaluation
        var payment = await WaitForPaymentStatusAsync(paymentClient, createdPayment.Id, "Approved", TimeSpan.FromSeconds(60));
        output.WriteLine($"Payment Status: {payment.Status} (Expected: Approved)");
    }

    [Fact]
    public async Task EventFlow_HighValuePayment_ShouldBeRejected()
    {
        // Arrange
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

        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("riskservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        paymentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        // Act: Create a high-value payment
        var highValueRequest = new
        {
            fromUserId = "user_charlie",
            toUserId = "user_diana",
            amount = 15000.00m,  // > 10,000 threshold
            currency = "USD"
        };

        var createResponse = await paymentClient.PostAsJsonAsync("/Payments", highValueRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(createdPayment);

        output.WriteLine($"Created High-Value Payment: {createdPayment.Id}");

        // Assert: Verify payment is rejected
        var payment = await WaitForPaymentStatusAsync(paymentClient, createdPayment.Id, "Rejected", TimeSpan.FromSeconds(60));
        output.WriteLine($"Payment Status: {payment.Status} (Expected: Rejected due to high value)");
    }

    [Fact]
    public async Task EventFlow_SuspiciousUser_ShouldBeRejected()
    {
        // Arrange
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

        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("riskservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");

        // Act: Create payment from suspicious user
        var suspiciousRequest = new
        {
            fromUserId = "suspicious_actor_123",  // Matches "suspicious" prefix rule
            toUserId = "user_eve",
            amount = 1000.00m,
            currency = "USD"
        };

        var createResponse = await paymentClient.PostAsJsonAsync("/Payments", suspiciousRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(createdPayment);

        output.WriteLine($"Created Payment from Suspicious User: {createdPayment.Id}");

        // Assert: Verify payment is rejected
        var payment = await WaitForPaymentStatusAsync(paymentClient, createdPayment.Id, "Rejected", TimeSpan.FromSeconds(60));
        output.WriteLine($"Payment Status: {payment.Status} (Expected: Rejected due to suspicious user)");
    }

    [Fact]
    public async Task QueryRiskAssessment_ShouldReturnDetails()
    {
        // Arrange
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

        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("riskservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        var riskClient = app.CreateHttpClient("riskservice", "http");
        paymentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        riskClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        // Create a payment
        var paymentRequest = new
        {
            fromUserId = "user_frank",
            toUserId = "user_grace",
            amount = 750.00m,
            currency = "USD"
        };

        var createResponse = await paymentClient.PostAsJsonAsync("/Payments", paymentRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();
        Assert.NotNull(createdPayment);

        // Act: Query risk assessment
        var riskResponse = await WaitForRiskAssessmentAsync(riskClient, createdPayment.Id, TimeSpan.FromSeconds(60));

        // Assert: Verify assessment exists
        var assessment = await riskResponse.Content.ReadFromJsonAsync<RiskAssessmentDto>();
        Assert.NotNull(assessment);
        Assert.Equal(createdPayment.Id, assessment.PaymentId);
        Assert.NotNull(assessment.Reason);
        Assert.InRange(assessment.RiskScore, 0, 100);

        output.WriteLine($"Risk Assessment - Score: {assessment.RiskScore}, Approved: {assessment.IsApproved}, Reason: {assessment.Reason}");
    }

    [Fact]
    public async Task ListRiskAssessments_ShouldReturnPagedResults()
    {
        // Arrange
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

        await resourceNotifications.WaitForResourceAsync("paymentservice", KnownResourceStates.Running);
        await resourceNotifications.WaitForResourceAsync("riskservice", KnownResourceStates.Running);

        var paymentClient = app.CreateHttpClient("paymentservice", "http");
        var riskClient = app.CreateHttpClient("riskservice", "http");
        paymentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        riskClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        // Create multiple payments to populate risk assessments
        for (int i = 0; i < 3; i++)
        {
            var request = new
            {
                fromUserId = $"user_batch_{i}",
                toUserId = $"user_dest_{i}",
                amount = 500.00m + i * 100,
                currency = "USD"
            };

            var response = await paymentClient.PostAsJsonAsync("/Payments", request);
            response.EnsureSuccessStatusCode();
        }

        await WaitForRiskAssessmentListAsync(riskClient, minimumTotalCount: 3, TimeSpan.FromSeconds(60));

        // Act: List risk assessments with pagination
        var listResponse = await riskClient.GetAsync("/risks?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var result = await listResponse.Content.ReadFromJsonAsync<RiskAssessmentsPageDto>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 3, "Should have at least 3 assessments");
        Assert.NotEmpty(result.Assessments);

        output.WriteLine($"Risk Assessments - Total: {result.TotalCount}, Page Size: {result.Assessments.Count}");
    }

    #region DTOs

    private static async Task<PaymentDto> WaitForPaymentStatusAsync(HttpClient paymentClient, Guid paymentId, string expectedStatus, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var response = await paymentClient.GetAsync($"/Payments/{paymentId}");
                if (response.IsSuccessStatusCode)
                {
                    var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
                    if (payment != null && string.Equals(payment.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        return payment;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Payment {paymentId} did not reach status '{expectedStatus}' within {timeout.TotalSeconds} seconds.");
    }

    private static async Task<HttpResponseMessage> WaitForRiskAssessmentAsync(HttpClient riskClient, Guid paymentId, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var response = await riskClient.GetAsync($"/risks/{paymentId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response;
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Risk assessment for payment {paymentId} was not available within {timeout.TotalSeconds} seconds.");
    }

    private static async Task WaitForRiskAssessmentListAsync(HttpClient riskClient, int minimumTotalCount, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        HttpResponseMessage? lastResponse = null;
        string? lastBody = null;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                lastResponse = await riskClient.GetAsync("/risks?page=1&pageSize=10");
                if (lastResponse.IsSuccessStatusCode)
                {
                    lastBody = await lastResponse.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<RiskAssessmentsPageDto>(
                        lastBody,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result?.TotalCount >= minimumTotalCount)
                    {
                        return;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"Risk assessments list did not reach TotalCount >= {minimumTotalCount} within {timeout.TotalSeconds} seconds. " +
            $"Last HTTP {(int)(lastResponse?.StatusCode ?? 0)} {lastResponse?.StatusCode}. Body: {lastBody}");
    }

    private class PaymentDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string FromUserId { get; set; } = string.Empty;
        public string ToUserId { get; set; } = string.Empty;
    }

    private class RiskAssessmentDto
    {
        public Guid Id { get; set; }
        public Guid PaymentId { get; set; }
        public int RiskScore { get; set; }
        public bool IsApproved { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    private class RiskAssessmentsPageDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<RiskAssessmentDto> Assessments { get; set; } = [];
    }

    #endregion
}
