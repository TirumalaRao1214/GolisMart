using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AuthApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthApiClient(HttpClient http, ILogger<AuthApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/v1/auth/register", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, ct);
                return (true, null, auth);
            }

            var error = await ExtractErrorAsync(response, ct);
            return (false, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration request failed");
            return (false, "Registration is temporarily unavailable. Please try again.", null);
        }
    }

    public async Task<(bool Success, string? Error, AuthResponse? Response)> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/v1/auth/login", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, ct);
                return (true, null, auth);
            }

            var error = await ExtractErrorAsync(response, ct);
            return (false, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login request failed");
            return (false, "Login is temporarily unavailable. Please try again.", null);
        }
    }

    public async Task<MeResponse?> GetMeAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<MeResponse>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMe request failed");
            return null;
        }
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/customers/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<CustomerProfileDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProfile request failed");
            return null;
        }
    }

    public async Task<(bool Success, string? Error, CustomerProfileDto? Profile)> UpdateProfileAsync(
        string token, UpdateProfileRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, "api/v1/customers/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<CustomerProfileDto>(JsonOptions, ct);
                return (true, null, profile);
            }
            var error = await ExtractErrorAsync(response, ct);
            return (false, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateProfile request failed");
            return (false, "Profile update is temporarily unavailable.", null);
        }
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        string token, ChangePasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/customers/me/change-password");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode) return (true, null);
            var error = await ExtractErrorAsync(response, ct);
            return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChangePassword request failed");
            return (false, "Password change is temporarily unavailable.");
        }
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/customers/me/addresses");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<AddressDto>>(JsonOptions, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAddresses request failed");
            return [];
        }
    }

    public async Task<(bool Success, string? Error, AddressDto? Address)> CreateAddressAsync(
        string token, CreateAddressRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/customers/me/addresses");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var address = await response.Content.ReadFromJsonAsync<AddressDto>(JsonOptions, ct);
                return (true, null, address);
            }
            var error = await ExtractErrorAsync(response, ct);
            return (false, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateAddress request failed");
            return (false, "Address creation is temporarily unavailable.", null);
        }
    }

    public async Task<(bool Success, string? Error, AddressDto? Address)> UpdateAddressAsync(
        string token, int addressId, UpdateAddressRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/me/addresses/{addressId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var address = await response.Content.ReadFromJsonAsync<AddressDto>(JsonOptions, ct);
                return (true, null, address);
            }
            var error = await ExtractErrorAsync(response, ct);
            return (false, error, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAddress request failed");
            return (false, "Address update is temporarily unavailable.", null);
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAddressAsync(
        string token, int addressId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/customers/me/addresses/{addressId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, "Could not delete address.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAddress request failed");
            return (false, "Address deletion is temporarily unavailable.");
        }
    }

    public async Task<(bool Success, string? Error)> SetDefaultAddressAsync(
        string token, int addressId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/v1/customers/me/addresses/{addressId}/default");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, "Could not set default address.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetDefaultAddress request failed");
            return (false, "Operation is temporarily unavailable.");
        }
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me-roles");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUserRoles request failed");
            return [];
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return "An error occurred. Please try again.";

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // ValidationProblemDetails format
            if (root.TryGetProperty("errors", out var errors))
            {
                var messages = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                    foreach (var msg in prop.Value.EnumerateArray())
                        messages.Add(msg.GetString() ?? string.Empty);
                if (messages.Count > 0) return string.Join(" ", messages);
            }

            // ErrorResponse format
            if (root.TryGetProperty("error", out var err))
                return err.GetString() ?? "An error occurred.";

            // Problem details
            if (root.TryGetProperty("title", out var title))
                return title.GetString() ?? "An error occurred.";

            return "An error occurred. Please try again.";
        }
        catch
        {
            return "An error occurred. Please try again.";
        }
    }
}
