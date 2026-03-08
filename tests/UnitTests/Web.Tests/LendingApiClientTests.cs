using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using MercuryPay.Web;
using Xunit;

namespace MercuryPay.Web.Tests;

public class LendingApiClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly LendingApiClient _client;

    public LendingApiClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://lendingservice")
        };
        _client = new LendingApiClient(httpClient);
    }

    [Fact]
    public async Task CreateLoanAsync_ShouldReturnResponse_WhenApiReturnsSuccess()
    {
        // Arrange
        var request = new LoanRequestModel("user1", 1000m, "USD");
        var expectedResponse = new LoanResponseModel(Guid.NewGuid(), "user1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m, null);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.ToString().EndsWith("/loans")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        // Act
        var result = await _client.CreateLoanAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.Id, result.Id);
        Assert.Equal(expectedResponse.Status, result.Status);
    }

    [Fact]
    public async Task GetLoansAsync_ShouldReturnList_WhenApiReturnsSuccess()
    {
        // Arrange
        var userId = "user1";
        var expectedLoans = new List<LoanResponseModel>
        {
            new(Guid.NewGuid(), userId, 1000m, "USD", "Pending", DateTime.UtcNow, 12, 0.05m, null),
            new(Guid.NewGuid(), userId, 2000m, "USD", "Approved", DateTime.UtcNow, 24, 0.05m, null)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().Contains($"/loans/user/{userId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedLoans))
            });

        // Act
        var result = await _client.GetLoansAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, l => Assert.Equal(userId, l.UserId));
    }
}
