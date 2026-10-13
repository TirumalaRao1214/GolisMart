namespace GOLISMart.API.DTOs;

// ── Bootstrap ─────────────────────────────────────────────────────────────────

public record AdminBootstrapRequest(string Email, string Password, string DisplayName, string BootstrapSecret);

// ── Dashboard ─────────────────────────────────────────────────────────────────

public record DashboardSummaryDto(
    int TotalOrders, int PendingOrders, int ConfirmedOrders,
    int PreparingOrders, int OutForDeliveryOrders, int DeliveredOrders,
    decimal TodaySales, decimal WeekSales, decimal MonthSales,
    int LowStockCount, int ActiveProducts,
    IReadOnlyList<RecentOrderDto> RecentOrders,
    IReadOnlyList<LowStockItemDto> LowStockItems);

public record RecentOrderDto(int OrderId, string OrderNumber, string CustomerEmail,
    decimal Total, string Status, DateTime CreatedAt);

public record LowStockItemDto(int ProductId, string ProductName, string SKU,
    decimal QuantityOnHand, decimal LowStockThreshold);

// ── Product management ────────────────────────────────────────────────────────

public record AdminProductDto(
    int Id, string Name, string SKU, string? Description,
    int CategoryId, string? CategoryName, string UnitCode, string UnitLabel,
    decimal Price, decimal? OfferPrice, bool IsFeatured, bool IsActive,
    string? ImagePath, decimal QuantityOnHand, decimal ReservedQuantity,
    decimal LowStockThreshold, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateProductRequest(
    string Name, string SKU, string? Description,
    int CategoryId, string UnitCode, string UnitLabel,
    decimal Price, decimal? OfferPrice, bool IsFeatured,
    decimal InitialStock, decimal LowStockThreshold);

public record UpdateProductRequest(
    string Name, string? Description,
    int CategoryId, string UnitCode, string UnitLabel,
    decimal Price, decimal? OfferPrice, bool IsFeatured, bool IsActive,
    decimal LowStockThreshold);

// ── Category management ───────────────────────────────────────────────────────

public record AdminCategoryDto(int Id, string Name, string Slug, string? Description,
    string? ImagePath, int SortOrder, bool IsActive, int ProductCount, DateTime UpdatedAt);

public record CreateCategoryRequest(string Name, string Slug, string? Description, int SortOrder);
public record UpdateCategoryRequest(string Name, string? Description, int SortOrder, bool IsActive);

// ── Inventory ─────────────────────────────────────────────────────────────────

public record InventoryAdjustRequest(int ProductId, decimal QuantityChange, string Reason);
public record InventoryItemDto(int ProductId, string ProductName, string SKU,
    decimal QuantityOnHand, decimal ReservedQuantity, decimal LowStockThreshold,
    bool IsLowStock, DateTime UpdatedAt);

// ── Order admin ───────────────────────────────────────────────────────────────

public record AdminOrderListDto(
    int OrderId, string OrderNumber, string CustomerEmail, string CustomerName,
    decimal TotalAmount, string Status, string PaymentMethod, string PaymentStatus,
    DateTime CreatedAt, int ItemCount);

public record AdminOrderDetailDto(
    int OrderId, string OrderNumber, string CustomerEmail, string CustomerName,
    string Status, string PaymentMethod, string PaymentStatus,
    string DeliveryAddress, List<OrderItemSnapshot> Items,
    decimal SubTotal, decimal DeliveryFee, decimal DiscountAmount, decimal TotalAmount,
    string? Notes, List<OrderStatusHistoryDto> StatusHistory);

public record UpdateOrderStatusRequest(string NewStatus, string? Notes);

// ── Settings ──────────────────────────────────────────────────────────────────

public record StoreSettingDto(string Key, string Value, string DataType, string Category, string? Description);
public record UpdateSettingsRequest(List<SettingUpdateItem> Settings);
public record SettingUpdateItem(string Key, string Value);
