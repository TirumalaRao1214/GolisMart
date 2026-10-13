using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class AdminApiClient(HttpClient http, ILogger<AdminApiClient> logger) : IAdminApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpRequestMessage Req(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return "An error occurred. Please try again.";
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors))
            {
                var msgs = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                    foreach (var msg in prop.Value.EnumerateArray())
                        msgs.Add(msg.GetString() ?? string.Empty);
                if (msgs.Count > 0) return string.Join(" ", msgs);
            }
            if (root.TryGetProperty("error", out var err)) return err.GetString() ?? "An error occurred.";
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? "An error occurred.";
            return "An error occurred. Please try again.";
        }
        catch { return "An error occurred. Please try again."; }
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────

    public async Task<DashboardSummaryDto?> GetDashboardAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, "api/v1/admin/dashboard", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetDashboard failed"); return null; }
    }

    // ── Products ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<AdminProductDto>?> GetProductsAsync(string token, int page = 1, int pageSize = 20,
        string? search = null, int? categoryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        try
        {
            var qs = new StringBuilder($"api/v1/admin/products?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append($"&search={Uri.EscapeDataString(search)}");
            if (categoryId.HasValue) qs.Append($"&categoryId={categoryId}");
            if (isActive.HasValue) qs.Append($"&isActive={isActive.Value.ToString().ToLower()}");
            using var req = Req(HttpMethod.Get, qs.ToString(), token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<AdminProductDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetProducts failed"); return null; }
    }

    public async Task<AdminProductDto?> GetProductByIdAsync(string token, int id, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/admin/products/{id}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AdminProductDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetProductById {Id} failed", id); return null; }
    }

    public async Task<AdminProductDto?> CreateProductAsync(string token, MultipartFormDataContent formData, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "api/v1/admin/products", token);
            req.Content = formData;
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AdminProductDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "CreateProduct failed"); return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateProductAsync(string token, int id, MultipartFormDataContent formData, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Put, $"api/v1/admin/products/{id}", token);
            req.Content = formData;
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdateProduct {Id} failed", id); return (false, "Update failed."); }
    }

    public async Task<(bool Success, string? Error)> DeactivateProductAsync(string token, int id, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Delete, $"api/v1/admin/products/{id}", token);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "DeactivateProduct {Id} failed", id); return (false, "Deactivation failed."); }
    }

    // ── Categories ─────────────────────────────────────────────────────────────

    public async Task<PagedResult<AdminCategoryDto>?> GetCategoriesAsync(string token, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/admin/categories?page={page}&pageSize={pageSize}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<AdminCategoryDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetCategories failed"); return null; }
    }

    public async Task<(bool Success, string? Error, AdminCategoryDto? Category)> CreateCategoryAsync(
        string token, AdminCreateCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "api/v1/admin/categories", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return (true, null, await resp.Content.ReadFromJsonAsync<AdminCategoryDto>(JsonOptions, ct));
            return (false, await ExtractErrorAsync(resp, ct), null);
        }
        catch (Exception ex) { logger.LogError(ex, "CreateCategory failed"); return (false, "Create failed.", null); }
    }

    public async Task<(bool Success, string? Error)> UpdateCategoryAsync(string token, int id, AdminUpdateCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Put, $"api/v1/admin/categories/{id}", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdateCategory {Id} failed", id); return (false, "Update failed."); }
    }

    public async Task<(bool Success, string? Error)> ToggleCategoryActiveAsync(string token, int id, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Patch, $"api/v1/admin/categories/{id}/toggle-active", token);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "ToggleCategoryActive {Id} failed", id); return (false, "Operation failed."); }
    }

    // ── Inventory ──────────────────────────────────────────────────────────────

    public async Task<PagedResult<AdminInventoryItemDto>?> GetInventoryAsync(string token, int page = 1, int pageSize = 30,
        string? search = null, bool? lowStock = null, CancellationToken ct = default)
    {
        try
        {
            var qs = new StringBuilder($"api/v1/admin/inventory?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append($"&search={Uri.EscapeDataString(search)}");
            if (lowStock.HasValue) qs.Append($"&lowStock={lowStock.Value.ToString().ToLower()}");
            using var req = Req(HttpMethod.Get, qs.ToString(), token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<AdminInventoryItemDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetInventory failed"); return null; }
    }

    public async Task<(bool Success, string? Error)> AdjustInventoryAsync(string token, AdminInventoryAdjustRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "api/v1/admin/inventory/adjust", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "AdjustInventory failed"); return (false, "Adjustment failed."); }
    }

    // ── Orders ─────────────────────────────────────────────────────────────────

    public async Task<PagedResult<AdminOrderListDto>?> GetAdminOrdersAsync(string token, int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? paymentStatus = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        try
        {
            var qs = new StringBuilder($"api/v1/admin/orders?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append($"&search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(status)) qs.Append($"&status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(paymentStatus)) qs.Append($"&paymentStatus={Uri.EscapeDataString(paymentStatus)}");
            if (from.HasValue) qs.Append($"&from={from.Value:yyyy-MM-dd}");
            if (to.HasValue) qs.Append($"&to={to.Value:yyyy-MM-dd}");
            using var req = Req(HttpMethod.Get, qs.ToString(), token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PagedResult<AdminOrderListDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetAdminOrders failed"); return null; }
    }

    public async Task<AdminOrderDetailDto?> GetAdminOrderDetailAsync(string token, int orderId, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"api/v1/admin/orders/{orderId}", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AdminOrderDetailDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetAdminOrderDetail {Id} failed", orderId); return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateOrderStatusAsync(string token, int orderId, AdminUpdateOrderStatusRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, $"api/v1/admin/orders/{orderId}/status", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdateOrderStatus {Id} failed", orderId); return (false, "Status update failed."); }
    }

    // ── Settings ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StoreSettingDto>?> GetSettingsAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, "api/v1/admin/settings", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<StoreSettingDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetSettings failed"); return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateSettingsAsync(string token, AdminUpdateSettingsRequest request, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "api/v1/admin/settings", token);
            req.Content = JsonContent.Create(request);
            var resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, await ExtractErrorAsync(resp, ct));
        }
        catch (Exception ex) { logger.LogError(ex, "UpdateSettings failed"); return (false, "Settings update failed."); }
    }

    // ── Reports & Analytics Implementation ────────────────────────────────────

    private static string BuildReportsQuery(string basePath, string? filter, DateTime? start, DateTime? end, string? status = null)
    {
        var sb = new StringBuilder(basePath);
        var query = new List<string>();
        if (!string.IsNullOrEmpty(filter)) query.Add($"filter={Uri.EscapeDataString(filter)}");
        if (start.HasValue) query.Add($"start={Uri.EscapeDataString(start.Value.ToString("O"))}");
        if (end.HasValue) query.Add($"end={Uri.EscapeDataString(end.Value.ToString("O"))}");
        if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (query.Count > 0)
        {
            sb.Append('?').Append(string.Join('&', query));
        }
        return sb.ToString();
    }

    public async Task<AnalyticsDashboardDto?> GetReportsDashboardAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/dashboard", filter, start, end);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AnalyticsDashboardDto>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetReportsDashboard failed"); return null; }
    }

    public async Task<IReadOnlyList<NoSalesProductDto>?> GetNoSalesProductsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/no-sales", filter, start, end);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<NoSalesProductDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetNoSalesProducts failed"); return null; }
    }

    public async Task<IReadOnlyList<InventoryQuantityDto>?> GetInventoryQuantitiesReportAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = Req(HttpMethod.Get, "api/v1/admin/reports/inventory-quantities", token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<InventoryQuantityDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetInventoryQuantitiesReport failed"); return null; }
    }

    public async Task<IReadOnlyList<InventoryAdjustmentDto>?> GetInventoryAdjustmentsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/inventory-adjustments", filter, start, end);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<InventoryAdjustmentDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetInventoryAdjustments failed"); return null; }
    }

    public async Task<IReadOnlyList<OrderReportDto>?> GetOrdersReportAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, string? status = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/orders", filter, start, end, status);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<OrderReportDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetOrdersReport failed"); return null; }
    }

    public async Task<IReadOnlyList<PaymentFailureRefundDto>?> GetPaymentFailuresRefundsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/failures-refunds", filter, start, end);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<PaymentFailureRefundDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetPaymentFailuresRefunds failed"); return null; }
    }

    public async Task<IReadOnlyList<RepeatCustomerDto>?> GetRepeatCustomersReportAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
    {
        try
        {
            var url = BuildReportsQuery("api/v1/admin/reports/repeat-customers", filter, start, end);
            using var req = Req(HttpMethod.Get, url, token);
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<List<RepeatCustomerDto>>(JsonOptions, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "GetRepeatCustomersReport failed"); return null; }
    }
}
