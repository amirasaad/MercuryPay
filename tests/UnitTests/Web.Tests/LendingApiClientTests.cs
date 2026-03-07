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
        var expectedResponse = new LoanResponseModel(Guid.NewGuid(), "user1", 1000m, "USD", "Pending");

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
}
