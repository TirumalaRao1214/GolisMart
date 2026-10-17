using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GOLISMart.Web.Services.Implementations;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace GOLISMart.Web.Tests;

public class PaymentsApiClientTests
{
    private static PaymentsApiClient CreateClient(HttpResponseMessage mockResponse)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(mockResponse);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };

        return new PaymentsApiClient(httpClient, NullLogger<PaymentsApiClient>.Instance);
    }

    [Fact]
    public async Task CreateRazorpayOrderAsync_On503WithJsonError_ThrowsHttpRequestExceptionWithParsedMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"error\":\"Payment gateway is temporarily down.\"}")
        };
        var client = CreateClient(response);
        var request = new CreateRazorpayOrderRequest("session-1", 1, null, "idem-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CreateRazorpayOrderAsync("test-jwt", request));

        ex.Message.Should().Be("Payment gateway is temporarily down.");
        ex.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task CreateRazorpayOrderAsync_On503WithMessageJson_ThrowsHttpRequestExceptionWithParsedMessage()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"message\":\"Payment service unavailable.\"}")
        };
        var client = CreateClient(response);
        var request = new CreateRazorpayOrderRequest("session-1", 1, null, "idem-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CreateRazorpayOrderAsync("test-jwt", request));

        ex.Message.Should().Be("Payment service unavailable.");
    }

    [Fact]
    public async Task CreateRazorpayOrderAsync_On422_ThrowsInvalidOperationException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"error\":\"Product is out of stock.\"}")
        };
        var client = CreateClient(response);
        var request = new CreateRazorpayOrderRequest("session-1", 1, null, "idem-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateRazorpayOrderAsync("test-jwt", request));

        ex.Message.Should().Be("Product is out of stock.");
    }

    [Fact]
    public async Task CreateRazorpayOrderAsync_On401_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        var client = CreateClient(response);
        var request = new CreateRazorpayOrderRequest("session-1", 1, null, "idem-1");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            client.CreateRazorpayOrderAsync("test-jwt", request));
    }
}
