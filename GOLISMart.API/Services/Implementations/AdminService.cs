using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

public class AdminService : IAdminService
{
    private static readonly HashSet<string> AllowedImageTypes =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IAdminRepository _repo;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IAdminRepository repo,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<AdminService> logger)
    {
        _repo   = repo;
        _env    = env;
        _config = config;
        _logger = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken ct = default)
        => _repo.GetDashboardSummaryAsync(ct);

    // ── Orders ────────────────────────────────────────────────────────────────

    public Task<(IReadOnlyList<AdminOrderListDto> Orders, int TotalCount)> GetOrdersAsync(
        string? search, string? status, string? paymentStatus,
        DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
        => _repo.GetOrdersAsync(search, status, paymentStatus, from, to, page, pageSize, ct);

    public Task<AdminOrderDetailDto?> GetOrderDetailAsync(int orderId, CancellationToken ct = default)
        => _repo.GetOrderDetailAsync(orderId, ct);

    public async Task<(bool Success, string? Error)> UpdateOrderStatusAsync(
        int orderId, string newStatus, string adminId, string? notes, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateOrderStatusAsync(orderId, newStatus, adminId, notes, ct);
        if (!updated)
            return (false, $"Order not found or status transition to '{newStatus}' is not allowed.");

        await _repo.LogAdminActionAsync(adminId, "UpdateOrderStatus", "Order", orderId.ToString(),
            $"Status changed to {newStatus}", ct);
        return (true, null);
    }

    // ── Products ──────────────────────────────────────────────────────────────

    public Task<(IReadOnlyList<AdminProductDto> Products, int TotalCount)> GetProductsAsync(
        string? search, int? categoryId, bool? isActive, int page, int pageSize, CancellationToken ct = default)
        => _repo.GetProductsAsync(search, categoryId, isActive, page, pageSize, ct);

    public Task<AdminProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default)
        => _repo.GetProductByIdAsync(id, ct);

    public async Task<(bool Success, string? Error, int ProductId)> CreateProductAsync(
        CreateProductRequest request, IFormFile? image, string adminId, CancellationToken ct = default)
    {
        // Validate SKU uniqueness
        if (!await _repo.IsSkuAvailableAsync(request.SKU, null, ct))
            return (false, $"SKU '{request.SKU}' is already in use.", 0);

        // Save image if provided
        string? imagePath = null;
        if (image is not null)
        {
            var (saved, error) = await SaveProductImageAsync(image);
            if (error is not null) return (false, error, 0);
            imagePath = saved;
        }

        var productId = await _repo.CreateProductAsync(request, imagePath, ct);
        await _repo.LogAdminActionAsync(adminId, "CreateProduct", "Product", productId.ToString(),
            $"Created product '{request.Name}' SKU={request.SKU}", ct);

        return (true, null, productId);
    }

    public async Task<(bool Success, string? Error)> UpdateProductAsync(
        int id, UpdateProductRequest request, IFormFile? image, string adminId, CancellationToken ct = default)
    {
        string? imagePath = null;
        if (image is not null)
        {
            var (saved, error) = await SaveProductImageAsync(image);
            if (error is not null) return (false, error);
            imagePath = saved;
        }

        var updated = await _repo.UpdateProductAsync(id, request, imagePath, ct);
        if (!updated) return (false, "Product not found.");

        await _repo.LogAdminActionAsync(adminId, "UpdateProduct", "Product", id.ToString(),
            $"Updated product '{request.Name}'", ct);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeactivateProductAsync(
        int id, string adminId, CancellationToken ct = default)
    {
        var deactivated = await _repo.DeactivateProductAsync(id, ct);
        if (!deactivated) return (false, "Product not found.");

        await _repo.LogAdminActionAsync(adminId, "DeactivateProduct", "Product", id.ToString(), null, ct);
        return (true, null);
    }

    // ── Categories ────────────────────────────────────────────────────────────

    public Task<(IReadOnlyList<AdminCategoryDto> Categories, int TotalCount)> GetCategoriesAsync(
        int page, int pageSize, CancellationToken ct = default)
        => _repo.GetCategoriesAsync(page, pageSize, ct);

    public async Task<(bool Success, string? Error, int CategoryId)> CreateCategoryAsync(
        CreateCategoryRequest request, string adminId, CancellationToken ct = default)
    {
        if (!await _repo.IsCategorySlugAvailableAsync(request.Slug, null, ct))
            return (false, $"Slug '{request.Slug}' is already in use.", 0);

        var categoryId = await _repo.CreateCategoryAsync(request, ct);
        await _repo.LogAdminActionAsync(adminId, "CreateCategory", "Category", categoryId.ToString(),
            $"Created category '{request.Name}' slug={request.Slug}", ct);

        return (true, null, categoryId);
    }

    public async Task<(bool Success, string? Error)> UpdateCategoryAsync(
        int id, UpdateCategoryRequest request, string adminId, CancellationToken ct = default)
    {
        var updated = await _repo.UpdateCategoryAsync(id, request, ct);
        if (!updated) return (false, "Category not found.");

        await _repo.LogAdminActionAsync(adminId, "UpdateCategory", "Category", id.ToString(),
            $"Updated category '{request.Name}'", ct);
        return (true, null);
    }

    // ── Inventory ─────────────────────────────────────────────────────────────

    public Task<(IReadOnlyList<InventoryItemDto> Items, int TotalCount)> GetInventoryAsync(
        string? search, bool? lowStock, int page, int pageSize, CancellationToken ct = default)
        => _repo.GetInventoryAsync(search, lowStock, page, pageSize, ct);

    public async Task<(bool Success, string? Error)> AdjustInventoryAsync(
        InventoryAdjustRequest request, string adminId, CancellationToken ct = default)
    {
        try
        {
            await _repo.AdjustInventoryAsync(request, adminId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }

        await _repo.LogAdminActionAsync(adminId, "AdjustInventory", "Product", request.ProductId.ToString(),
            $"Qty change: {request.QuantityChange:+0;-0}, reason: {request.Reason}", ct);
        return (true, null);
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<StoreSettingDto>> GetSettingsAsync(CancellationToken ct = default)
        => _repo.GetSettingsAsync(ct);

    public async Task<(bool Success, string? Error)> UpdateSettingsAsync(
        UpdateSettingsRequest request, string adminId, CancellationToken ct = default)
    {
        if (request.Settings is null || request.Settings.Count == 0)
            return (false, "No settings provided.");

        await _repo.UpdateSettingsAsync(request, adminId, ct);
        await _repo.LogAdminActionAsync(adminId, "UpdateSettings", null, null,
            $"Updated {request.Settings.Count} setting(s)", ct);
        return (true, null);
    }

    // ── Image handling ────────────────────────────────────────────────────────

    /// <summary>
    /// Saves an uploaded product image to the configured storage path.
    /// Returns the relative URL path or an error message.
    /// </summary>
    private async Task<(string? Path, string? Error)> SaveProductImageAsync(IFormFile image)
    {
        if (image.Length > MaxImageBytes)
            return (null, "Image must be smaller than 5 MB.");

        var ext = Path.GetExtension(image.FileName);
        if (!AllowedImageTypes.Contains(ext))
            return (null, "Only .jpg, .jpeg, .png, and .webp images are allowed.");

        // Resolve storage root from config, defaulting to content root + uploads/products
        var storagePath = _config["Storage:ProductImagesPath"]
            ?? Path.Combine(_env.ContentRootPath, "uploads", "products");

        Directory.CreateDirectory(storagePath);

        var fileName  = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var filePath  = Path.Combine(storagePath, fileName);

        await using var stream = File.Create(filePath);
        await image.CopyToAsync(stream);

        // Return a relative path suitable for storing in the DB and serving via the API
        return ($"/images/products/{fileName}", null);
    }

    // ── Reports & Analytics ───────────────────────────────────────────────────

    public Task<AnalyticsDashboardDto> GetAnalyticsDashboardAsync(DateTime startDate, DateTime endDate, string? filterType, CancellationToken ct = default)
        => _repo.GetAnalyticsDashboardAsync(startDate, endDate, filterType, ct);

    public Task<IReadOnlyList<NoSalesProductDto>> GetNoSalesProductsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        => _repo.GetNoSalesProductsAsync(startDate, endDate, ct);

    public Task<IReadOnlyList<InventoryQuantityDto>> GetInventoryQuantitiesReportAsync(CancellationToken ct = default)
        => _repo.GetInventoryQuantitiesReportAsync(ct);

    public Task<IReadOnlyList<InventoryAdjustmentDto>> GetInventoryAdjustmentsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        => _repo.GetInventoryAdjustmentsAsync(startDate, endDate, ct);

    public Task<IReadOnlyList<OrderReportDto>> GetOrdersReportAsync(DateTime startDate, DateTime endDate, string? status, CancellationToken ct = default)
        => _repo.GetOrdersReportAsync(startDate, endDate, status, ct);

    public Task<IReadOnlyList<PaymentFailureRefundDto>> GetPaymentFailuresRefundsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        => _repo.GetPaymentFailuresRefundsAsync(startDate, endDate, ct);

    public Task<IReadOnlyList<RepeatCustomerDto>> GetRepeatCustomersReportAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
        => _repo.GetRepeatCustomersReportAsync(startDate, endDate, ct);
}
