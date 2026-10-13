using GOLISMart.API.DTOs;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IAdminRepository
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);

    Task<(IReadOnlyList<AdminOrderListDto> Orders, int TotalCount)> GetOrdersAsync(
        string? search, string? status, string? paymentStatus,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken ct = default);

    Task<AdminOrderDetailDto?> GetOrderDetailAsync(int orderId, CancellationToken ct = default);

    Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus, string adminUserId, string? notes, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminProductDto> Products, int TotalCount)> GetProductsAsync(
        string? search, int? categoryId, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<AdminProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default);

    Task<int> CreateProductAsync(CreateProductRequest request, string? imagePath, CancellationToken ct = default);

    Task<bool> UpdateProductAsync(int id, UpdateProductRequest request, string? newImagePath, CancellationToken ct = default);

    Task<bool> DeactivateProductAsync(int id, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminCategoryDto> Categories, int TotalCount)> GetCategoriesAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<int> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);

    Task<bool> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default);

    Task<(IReadOnlyList<InventoryItemDto> Items, int TotalCount)> GetInventoryAsync(
        string? search, bool? lowStock, int page, int pageSize, CancellationToken ct = default);

    Task AdjustInventoryAsync(InventoryAdjustRequest request, string adminUserId, CancellationToken ct = default);

    Task<IReadOnlyList<StoreSettingDto>> GetSettingsAsync(CancellationToken ct = default);

    Task UpdateSettingsAsync(UpdateSettingsRequest request, string adminUserId, CancellationToken ct = default);

    Task<bool> IsSkuAvailableAsync(string sku, int? excludeProductId, CancellationToken ct = default);

    Task<bool> IsCategorySlugAvailableAsync(string slug, int? excludeCategoryId, CancellationToken ct = default);

    Task<bool> CategoryHasProductsAsync(int categoryId, CancellationToken ct = default);

    Task LogAdminActionAsync(string adminUserId, string action, string? entityType, string? entityId, string? details, CancellationToken ct = default);

    // ── Reports & Analytics ───────────────────────────────────────────────────
    Task<AnalyticsDashboardDto> GetAnalyticsDashboardAsync(DateTime startDate, DateTime endDate, string? filterType, CancellationToken ct = default);
    Task<IReadOnlyList<NoSalesProductDto>> GetNoSalesProductsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryQuantityDto>> GetInventoryQuantitiesReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventoryAdjustmentDto>> GetInventoryAdjustmentsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<OrderReportDto>> GetOrdersReportAsync(DateTime startDate, DateTime endDate, string? status, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentFailureRefundDto>> GetPaymentFailuresRefundsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<RepeatCustomerDto>> GetRepeatCustomersReportAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
