using GOLISMart.API.DTOs;
using Microsoft.AspNetCore.Http;

namespace GOLISMart.API.Services.Interfaces;

public interface IAdminService
{
    Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken ct = default);

    Task<(IReadOnlyList<AdminOrderListDto> Orders, int TotalCount)> GetOrdersAsync(
        string? search, string? status, string? paymentStatus,
        DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default);

    Task<AdminOrderDetailDto?> GetOrderDetailAsync(int orderId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateOrderStatusAsync(
        int orderId, string newStatus, string adminId, string? notes, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminProductDto> Products, int TotalCount)> GetProductsAsync(
        string? search, int? categoryId, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<AdminProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default);

    Task<(bool Success, string? Error, int ProductId)> CreateProductAsync(
        CreateProductRequest request, IFormFile? image, string adminId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateProductAsync(
        int id, UpdateProductRequest request, IFormFile? image, string adminId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> DeactivateProductAsync(
        int id, string adminId, CancellationToken ct = default);

    Task<(IReadOnlyList<AdminCategoryDto> Categories, int TotalCount)> GetCategoriesAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<(bool Success, string? Error, int CategoryId)> CreateCategoryAsync(
        CreateCategoryRequest request, string adminId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateCategoryAsync(
        int id, UpdateCategoryRequest request, string adminId, CancellationToken ct = default);

    Task<(IReadOnlyList<InventoryItemDto> Items, int TotalCount)> GetInventoryAsync(
        string? search, bool? lowStock, int page, int pageSize, CancellationToken ct = default);

    Task<(bool Success, string? Error)> AdjustInventoryAsync(
        InventoryAdjustRequest request, string adminId, CancellationToken ct = default);

    Task<IReadOnlyList<StoreSettingDto>> GetSettingsAsync(CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateSettingsAsync(
        UpdateSettingsRequest request, string adminId, CancellationToken ct = default);

    // ── Reports & Analytics ───────────────────────────────────────────────────
    Task<AnalyticsDashboardDto> GetAnalyticsDashboardAsync(DateTime startDate, DateTime endDate, string? filterType, CancellationToken ct = default);
    Task<IReadOnlyList<NoSalesProductDto>> GetNoSalesProductsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryQuantityDto>> GetInventoryQuantitiesReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InventoryAdjustmentDto>> GetInventoryAdjustmentsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<OrderReportDto>> GetOrdersReportAsync(DateTime startDate, DateTime endDate, string? status, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentFailureRefundDto>> GetPaymentFailuresRefundsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<IReadOnlyList<RepeatCustomerDto>> GetRepeatCustomersReportAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
}
