using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class PaymentsApiClient : IPaymentsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PaymentsApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentsApiClient(HttpClient http, ILogger<PaymentsApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<CreateRazorpayOrderResponse?> CreateRazorpayOrderAsync(
        string token,
        CreateRazorpayOrderRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/payments/razorpay/create-order");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req, ct);
            var body     = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("CreateRazorpayOrder: API returned {Status}", (int)response.StatusCode);
                throw new UnauthorizedAccessException("Authentication required.");
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                response.StatusCode == HttpStatusCode.InternalServerError)
            {
                // 503 = payment service not configured  OR  gateway unreachable
                // 500 = unhandled exception inside the API (e.g. gateway failure bubbled up)
                _logger.LogError(
                    "[{CorrelationId}] CreateRazorpayOrder: API returned {Status} — {Body}",
                    Activity.Current?.Id ?? "n/a",
                    (int)response.StatusCode,
                    body.Length > 500 ? body[..500] : body);
                // Use a distinct marker so CheckoutController can return 503 (not 422)
                throw new HttpRequestException(
                    ExtractErrorMessage(body, "Payment service is currently unavailable. Please try again."),
                    null,
                    response.StatusCode);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity ||
                response.StatusCode == HttpStatusCode.BadRequest)
            {
                _logger.LogInformation(
                    "[{CorrelationId}] CreateRazorpayOrder: API returned {Status}: {Body}",
                    Activity.Current?.Id ?? "n/a",
                    (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                throw new InvalidOperationException(ExtractErrorMessage(body, "Unable to create payment order."));
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] CreateRazorpayOrder: API returned unexpected {Status}: {Body}",
                    Activity.Current?.Id ?? "n/a",
                    (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                throw new HttpRequestException(
                    "Payment service returned an unexpected error. Please try again.",
                    null,
                    response.StatusCode);
            }

            return JsonSerializer.Deserialize<CreateRazorpayOrderResponse>(body, JsonOptions);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (InvalidOperationException)   { throw; }
        catch (HttpRequestException)        { throw; }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] CreateRazorpayOrder request timed out",
                Activity.Current?.Id ?? "n/a");
            throw new HttpRequestException("Payment gateway request timed out. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] CreateRazorpayOrder request failed with unexpected exception",
                Activity.Current?.Id ?? "n/a");
            throw new HttpRequestException("Payment service encountered an unexpected error. Please try again.");
        }
    }

    public async Task<VerifyRazorpayPaymentResponse?> VerifyRazorpayPaymentAsync(
        string token,
        VerifyRazorpayPaymentRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/payments/razorpay/verify");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req, ct);
            var body     = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("VerifyRazorpayPayment: API returned {Status}", (int)response.StatusCode);
                throw new UnauthorizedAccessException("Authentication required.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("VerifyRazorpayPayment: API returned {Status}: {Body}",
                    (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                return null;
            }

            return JsonSerializer.Deserialize<VerifyRazorpayPaymentResponse>(body, JsonOptions);
        }
        catch (UnauthorizedAccessException) { throw; }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "VerifyRazorpayPayment request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyRazorpayPayment request failed");
            return null;
        }
    }

    private static string ExtractErrorMessage(string body, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && !string.IsNullOrWhiteSpace(err.GetString()))
                return err.GetString()!;
            if (doc.RootElement.TryGetProperty("message", out var msg) && !string.IsNullOrWhiteSpace(msg.GetString()))
                return msg.GetString()!;
        }
        catch { /* ignore */ }
        return fallback;
    }
}
