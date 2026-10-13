using GOLISMart.Web.Services;

namespace GOLISMart.Web.Services.Interfaces;

public interface IAdminApiClient
{
    // ── Dashboard ──────────────────────────────────────────────────────────────
    Task<DashboardSummaryDto?> GetDashboardAsync(string token, CancellationToken ct = default);

    // ── Products ───────────────────────────────────────────────────────────────
    Task<PagedResult<AdminProductDto>?> GetProductsAsync(string token, int page = 1, int pageSize = 20,
        string? search = null, int? categoryId = null, bool? isActive = null, CancellationToken ct = default);
    Task<AdminProductDto?> GetProductByIdAsync(string token, int id, CancellationToken ct = default);
    Task<AdminProductDto?> CreateProductAsync(string token, MultipartFormDataContent formData, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateProductAsync(string token, int id, MultipartFormDataContent formData, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeactivateProductAsync(string token, int id, CancellationToken ct = default);

    // ── Categories ─────────────────────────────────────────────────────────────
    Task<PagedResult<AdminCategoryDto>?> GetCategoriesAsync(string token, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<(bool Success, string? Error, AdminCategoryDto? Category)> CreateCategoryAsync(string token, AdminCreateCategoryRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateCategoryAsync(string token, int id, AdminUpdateCategoryRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ToggleCategoryActiveAsync(string token, int id, CancellationToken ct = default);

    // ── Inventory ──────────────────────────────────────────────────────────────
    Task<PagedResult<AdminInventoryItemDto>?> GetInventoryAsync(string token, int page = 1, int pageSize = 30,
        string? search = null, bool? lowStock = null, CancellationToken ct = default);
    Task<(bool Success, string? Error)> AdjustInventoryAsync(string token, AdminInventoryAdjustRequest request, CancellationToken ct = default);

    // ── Orders ─────────────────────────────────────────────────────────────────
    Task<PagedResult<AdminOrderListDto>?> GetAdminOrdersAsync(string token, int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? paymentStatus = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<AdminOrderDetailDto?> GetAdminOrderDetailAsync(string token, int orderId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateOrderStatusAsync(string token, int orderId, AdminUpdateOrderStatusRequest request, CancellationToken ct = default);

    // ── Settings ───────────────────────────────────────────────────────────────
    Task<IReadOnlyList<StoreSettingDto>?> GetSettingsAsync(string token, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateSettingsAsync(string token, AdminUpdateSettingsRequest request, CancellationToken ct = default);

    // ── Reports & Analytics ─────────────────────────────────────────────────────
    Task<AnalyticsDashboardDto?> GetReportsDashboardAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default);
    Task<IReadOnlyList<NoSalesProductDto>?> GetNoSalesProductsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryQuantityDto>?> GetInventoryQuantitiesReportAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryAdjustmentDto>?> GetInventoryAdjustmentsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default);
    Task<IReadOnlyList<OrderReportDto>?> GetOrdersReportAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, string? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentFailureRefundDto>?> GetPaymentFailuresRefundsAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default);
    Task<IReadOnlyList<RepeatCustomerDto>?> GetRepeatCustomersReportAsync(string token, string? filter = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default);
}
