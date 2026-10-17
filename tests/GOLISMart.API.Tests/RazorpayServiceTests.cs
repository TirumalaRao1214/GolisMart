using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GOLISMart.API.Services.Implementations;
using Xunit;

namespace GOLISMart.API.Tests;

public class RazorpayServiceTests
{
    [Fact]
    public void ComputeHmacSha256_ComputesCorrectDigest()
    {
        // Arrange
        var data = "order_O12345678|pay_P12345678";
        var secret = "test_secret_key_123";

        // Expected HMAC-SHA256
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var expected = Convert.ToHexString(HMACSHA256.HashData(keyBytes, dataBytes)).ToLowerInvariant();

        // Act
        var result = RazorpayService.ComputeHmacSha256(data, secret);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidateWebhookSignature_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var payload = "{\"event\":\"payment.captured\",\"id\":\"evt_123\"}";
        var secret = "webhook_secret_key";
        var signature = RazorpayService.ComputeHmacSha256(payload, secret);

        // Act
        var isValid = RazorpayService.ValidateWebhookSignature(payload, signature, secret);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhookSignature_InvalidSignature_ReturnsFalse()
    {
        // Arrange
        var payload = "{\"event\":\"payment.captured\",\"id\":\"evt_123\"}";
        var secret = "webhook_secret_key";
        var signature = "invalid_signature_hex_value";

        // Act
        var isValid = RazorpayService.ValidateWebhookSignature(payload, signature, secret);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task RazorpayApi_CreateOrderDirect_Test()
    {
        var keyId = "rzp_test_ToFZ5mdbh7G3Fw";
        var keySecret = "q06B06FFaDlJcVxFOa7yECLx";
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));

        using var client = new System.Net.Http.HttpClient();
        client.BaseAddress = new Uri("https://api.razorpay.com/");

        using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "v1/orders")
        {
            Content = new System.Net.Http.StringContent("{\"amount\":1000,\"currency\":\"INR\",\"receipt\":\"test_rcpt\"}", Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var res = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            throw new Exception($"Failed: {(int)res.StatusCode} - {body}");
        }
    }
}
