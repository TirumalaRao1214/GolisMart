using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class DeliveryManagementApiClient(HttpClient http, ILogger<DeliveryManagementApiClient> logger)
    : IDeliveryManagementApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private HttpRequestMessage Req(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return "An error occurred.";
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var m)) return m.GetString() ?? "An error occurred.";
            if (root.TryGetProperty("title",   out var t)) return t.GetString() ?? "An error occurred.";
            return "An error occurred.";
        }
        catch { return "An error occurred."; }
    }

    // ── Admin: Delivery Orders ─────────────────────────────────────────────────

    public async Task<PagedResult<AdminDeliveryOrderListDto>?> GetDeliveryOrdersAsync(
        string token, int page = 1, int pageSize = 20,
        string? search = null, string? status = null,
        DateTime? from = null, DateTime? to = null,
        string? pincode = null, int? personnelId = null,
        CancellationToken ct = default)
    {
        try
        {
            var qs = new StringBuilder($"api/v1/admin/delivery/orders?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(search))    qs.Append($"&search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(status))    qs.Append($"&status={Uri.EscapeDataString(status)}");
            if (from.HasValue)                         qs.Append($"&from={from.Value:yyyy-MM-dd}");
            if (to.HasValue)                           qs.Append($"&to={to.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(pincode))   qs.Append($"&pincode={Uri.EscapeDataString(pincode)}");
            if (personnelId.HasValue)                  qs.Append($"&personnelId={personnelId}");
            using var req = Req(HttpMethod.Get, qs.ToString(), token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<AdminDeliveryOrderListDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetDeliveryOrders failed"); return null; }
    }

    public async Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(
        string token, int orderId, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/admin/delivery/orders/{orderId}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AdminDeliveryOrderDetailDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetDeliveryOrderDetail {Id} failed", orderId); return null; }
    }

    public async Task<(bool Success, string? Error)> AssignOrderAsync(
        string token, int orderId, AdminAssignDeliveryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, $"api/v1/admin/delivery/orders/{orderId}/assign", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "AssignOrder {Id} failed", orderId); return (false, "Assignment failed."); }
    }

    public async Task<(bool Success, string? Error)> UnassignOrderAsync(
        string token, int orderId, AdminUnassignDeliveryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, $"api/v1/admin/delivery/orders/{orderId}/unassign", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UnassignOrder {Id} failed", orderId); return (false, "Unassign failed."); }
    }

    // ── Admin: Delivery Personnel ──────────────────────────────────────────────

    public async Task<PagedResult<DeliveryPersonnelDto>?> GetPersonnelAsync(
        string token, int page = 1, int pageSize = 20,
        string? search = null, bool? isActive = null, CancellationToken ct = default)
    {
        try
        {
            var qs = new StringBuilder($"api/v1/admin/delivery/personnel?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append($"&search={Uri.EscapeDataString(search)}");
            if (isActive.HasValue) qs.Append($"&isActive={isActive.Value.ToString().ToLower()}");
            using var req = Req(HttpMethod.Get, qs.ToString(), token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<DeliveryPersonnelDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetPersonnel failed"); return null; }
    }

    public async Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(
        string token, int id, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/admin/delivery/personnel/{id}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DeliveryPersonnelDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetPersonnelById {Id} failed", id); return null; }
    }

    public async Task<(bool Success, string? Error, DeliveryPersonnelDto? Personnel)> CreatePersonnelAsync(
        string token, AdminCreatePersonnelRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "api/v1/admin/delivery/personnel", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return (true, null, await resp.Content.ReadFromJsonAsync<DeliveryPersonnelDto>(JsonOptions, ct));
            return (false, await ExtractErrorAsync(resp, ct), null);
        }
        catch (Exception ex) { logger.LogError(ex, "CreatePersonnel failed"); return (false, "Create failed.", null); }
    }

    public async Task<(bool Success, string? Error)> UpdatePersonnelAsync(
        string token, int id, AdminUpdatePersonnelRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Put, $"api/v1/admin/delivery/personnel/{id}", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdatePersonnel {Id} failed", id); return (false, "Update failed."); }
    }

    // ── Delivery Person ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DeliveryAssignmentDto>?> GetMyAssignmentsAsync(
        string token, string? status = null, CancellationToken ct = default)
    {
        try
        {
            var qs = "api/v1/deliveries/my";
            if (!string.IsNullOrWhiteSpace(status)) qs += $"?status={Uri.EscapeDataString(status)}";
            using var req = Req(HttpMethod.Get, qs, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<DeliveryAssignmentDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetMyAssignments failed"); return null; }
    }

    public async Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        string token, int orderId, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/deliveries/my/{orderId}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DeliveryAssignmentDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetMyAssignmentDetail {Id} failed", orderId); return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        string token, int orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, $"api/v1/deliveries/my/{orderId}/status", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdateDeliveryStatus {Id} failed", orderId); return (false, "Update failed."); }
    }

    public async Task<(bool Success, string? Error)> ConfirmDeliveredAsync(
        string token, int orderId, ConfirmDeliveredRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, $"api/v1/deliveries/my/{orderId}/confirm-delivered", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "ConfirmDelivered {Id} failed", orderId); return (false, "Confirmation failed."); }
    }

    // ── Customer ───────────────────────────────────────────────────────────────

    public async Task<DeliveryTrackingDto?> GetTrackingAsync(
        string token, int orderId, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/orders/{orderId}/tracking", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DeliveryTrackingDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetTracking {Id} failed", orderId); return null; }
    }

    public async Task<IReadOnlyList<InAppNotificationDto>?> GetNotificationsAsync(
        string token, int limit = 20, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/orders/notifications?limit={limit}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<InAppNotificationDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetNotifications failed"); return null; }
    }
}
