namespace GOLISMart.Web.Services;

// ---------------------------------------------------------------------------
// Admin DTOs — mirror of GOLISMart.API.DTOs.AdminDto.
// No project reference to the API is used; these are maintained separately.
// ---------------------------------------------------------------------------

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

public record AdminCreateProductRequest(
    string Name, string SKU, string? Description,
    int CategoryId, string UnitCode, string UnitLabel,
    decimal Price, decimal? OfferPrice, bool IsFeatured,
    decimal InitialStock, decimal LowStockThreshold);

public record AdminUpdateProductRequest(
    string Name, string? Description,
    int CategoryId, string UnitCode, string UnitLabel,
    decimal Price, decimal? OfferPrice, bool IsFeatured, bool IsActive,
    decimal LowStockThreshold);

// ── Category management ───────────────────────────────────────────────────────

public record AdminCategoryDto(int Id, string Name, string Slug, string? Description,
    string? ImagePath, int SortOrder, bool IsActive, int ProductCount, DateTime UpdatedAt);

public record AdminCreateCategoryRequest(string Name, string Slug, string? Description, int SortOrder);
public record AdminUpdateCategoryRequest(string Name, string? Description, int SortOrder, bool IsActive);

// ── Inventory ─────────────────────────────────────────────────────────────────

public record AdminInventoryAdjustRequest(int ProductId, decimal QuantityChange, string Reason);
public record AdminInventoryItemDto(int ProductId, string ProductName, string SKU,
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

public record AdminUpdateOrderStatusRequest(string NewStatus, string? Notes);

// ── Settings ──────────────────────────────────────────────────────────────────

public record StoreSettingDto(string Key, string Value, string DataType, string Category, string? Description);
public record AdminUpdateSettingsRequest(List<AdminSettingUpdateItem> Settings);
public record AdminSettingUpdateItem(string Key, string Value);

// ── Reports & Analytics ───────────────────────────────────────────────────────

public record ReportFilterDto(
    DateTime StartDate,
    DateTime EndDate,
    string? FilterType
);

public record AnalyticsDashboardDto(
    ReportFilterDto Filter,
    decimal GrossSales,
    decimal NetSales,
    decimal TotalDiscounts,
    decimal TotalDeliveryFees,
    int TotalOrders,
    int CompletedOrders,
    int PendingOrders,
    int CancelledOrders,
    decimal AverageOrderValue,
    int CodOrdersCount,
    decimal CodNetSales,
    int OnlineOrdersCount,
    decimal OnlineNetSales,
    int LowStockCount,
    int OutOfStockCount,
    int NewCustomersCount,
    int RepeatCustomersCount,
    List<DailySalesTrendDto> DailySalesTrends,
    List<CategorySalesDto> SalesByCategory,
    List<PaymentMethodDistributionDto> PaymentMethodDistribution,
    List<ProductSalesPerformanceDto> TopSellingProducts
);

public record DailySalesTrendDto(
    DateTime Date,
    decimal NetSales,
    int OrderCount
);

public record CategorySalesDto(
    int CategoryId,
    string CategoryName,
    decimal NetSales,
    int QuantitySold
);

public record PaymentMethodDistributionDto(
    string PaymentMethod,
    int OrderCount,
    decimal NetSales
);

public record ProductSalesPerformanceDto(
    int ProductId,
    string ProductName,
    string SKU,
    string CategoryName,
    decimal QuantitySold,
    decimal NetSales,
    decimal AverageUnitPrice
);

public record NoSalesProductDto(
    int ProductId,
    string SKU,
    string ProductName,
    string CategoryName,
    decimal Price,
    decimal QuantityOnHand
);

public record InventoryQuantityDto(
    int ProductId,
    string ProductName,
    string SKU,
    string CategoryName,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal LowStockThreshold,
    bool IsLowStock,
    bool IsOutOfStock
);

public record InventoryAdjustmentDto(
    int Id,
    int ProductId,
    string ProductName,
    string SKU,
    string TransactionType,
    decimal QuantityChange,
    decimal QuantityAfter,
    string? Notes,
    DateTime CreatedAt,
    string? CreatedBy
);

public record OrderReportDto(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    DateTime CreatedAt,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    decimal TotalAmount
);

public record PaymentFailureRefundDto(
    int OrderId,
    string OrderNumber,
    decimal TotalAmount,
    string OrderStatus,
    string PaymentStatus,
    string PaymentMethod,
    DateTime CreatedAt,
    string? GatewayName,
    string? PaymentStatusDetail,
    string? FailureReason
);

public record RepeatCustomerDto(
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    int TotalOrders,
    decimal TotalSpent,
    decimal AverageOrderValue,
    DateTime FirstOrderDate,
    DateTime LastOrderDate
);
