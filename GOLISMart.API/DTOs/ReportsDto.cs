using System;
using System.Collections.Generic;

namespace GOLISMart.API.DTOs;

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
