using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminReportsController(IAdminApiClient admin, ILogger<AdminReportsController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private string? Token => HttpContext?.Request?.Cookies[TokenCookie];

    [HttpGet("admin/reports")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        ViewData["Title"] = "Reports & Business Insights";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var initial = await admin.GetReportsDashboardAsync(token, "last30", null, null, ct);
            return View("~/Views/Admin/Reports/Index.cshtml", initial);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin reports index failed to load");
            return View("~/Views/Admin/Reports/Index.cshtml", null);
        }
    }

    [HttpGet("admin/reports/api/dashboard")]
    [Produces("application/json")]
    public async Task<IActionResult> GetDashboardData(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetReportsDashboardAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load dashboard data." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/no-sales")]
    [Produces("application/json")]
    public async Task<IActionResult> GetNoSalesData(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetNoSalesProductsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load no-sales products." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/inventory-quantities")]
    [Produces("application/json")]
    public async Task<IActionResult> GetInventoryQuantitiesData(CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetInventoryQuantitiesReportAsync(token, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load inventory report." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/inventory-adjustments")]
    [Produces("application/json")]
    public async Task<IActionResult> GetInventoryAdjustmentsData(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetInventoryAdjustmentsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load inventory adjustments." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/orders")]
    [Produces("application/json")]
    public async Task<IActionResult> GetOrdersData(string? filter, DateTime? start, DateTime? end, string? status, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetOrdersReportAsync(token, filter, start, end, status, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load orders report." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/failures-refunds")]
    [Produces("application/json")]
    public async Task<IActionResult> GetFailuresRefundsData(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetPaymentFailuresRefundsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load failures/refunds report." });

        return Json(data);
    }

    [HttpGet("admin/reports/api/repeat-customers")]
    [Produces("application/json")]
    public async Task<IActionResult> GetRepeatCustomersData(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return Unauthorized(new { Error = "Unauthorized access." });

        var data = await admin.GetRepeatCustomersReportAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest(new { Error = "Failed to load repeat customers report." });

        return Json(data);
    }

    // ── CSV Export Actions ────────────────────────────────────────────────────

    [HttpGet("admin/reports/export/sales")]
    public async Task<IActionResult> ExportSales(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetReportsDashboardAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Date,Net Sales,Order Count");
        foreach (var trend in data.DailySalesTrends)
        {
            csv.AppendLine($"{trend.Date:yyyy-MM-dd},{trend.NetSales},{trend.OrderCount}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"sales_trend_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/orders")]
    public async Task<IActionResult> ExportOrders(string? filter, DateTime? start, DateTime? end, string? status, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetOrdersReportAsync(token, filter, start, end, status, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Order Number,Customer Name,Customer Email,Date,Status,Payment Method,Payment Status,Total Amount");
        foreach (var order in data)
        {
            csv.AppendLine($"{EscapeCsvField(order.OrderNumber)},{EscapeCsvField(order.CustomerName)},{EscapeCsvField(order.CustomerEmail)},{order.CreatedAt:yyyy-MM-dd HH:mm:ss},{EscapeCsvField(order.Status)},{EscapeCsvField(order.PaymentMethod)},{EscapeCsvField(order.PaymentStatus)},{order.TotalAmount}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"orders_report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/products")]
    public async Task<IActionResult> ExportProducts(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetReportsDashboardAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Product Name,SKU,Category,Quantity Sold,Net Sales,Average Unit Price");
        foreach (var p in data.TopSellingProducts)
        {
            csv.AppendLine($"{EscapeCsvField(p.ProductName)},{EscapeCsvField(p.SKU)},{EscapeCsvField(p.CategoryName)},{p.QuantitySold},{p.NetSales},{p.AverageUnitPrice}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"product_performance_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/inventory")]
    public async Task<IActionResult> ExportInventory(CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetInventoryQuantitiesReportAsync(token, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Product Name,SKU,Category,Quantity On Hand,Reserved Quantity,Low Stock Threshold,Is Low Stock,Is Out Of Stock");
        foreach (var i in data)
        {
            csv.AppendLine($"{EscapeCsvField(i.ProductName)},{EscapeCsvField(i.SKU)},{EscapeCsvField(i.CategoryName)},{i.QuantityOnHand},{i.ReservedQuantity},{i.LowStockThreshold},{(i.IsLowStock ? "Yes" : "No")},{(i.IsOutOfStock ? "Yes" : "No")}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"inventory_quantities_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/adjustments")]
    public async Task<IActionResult> ExportInventoryAdjustments(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetInventoryAdjustmentsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Date,Product Name,SKU,Type,Quantity Change,Quantity After,Notes,Created By");
        foreach (var tx in data)
        {
            csv.AppendLine($"{tx.CreatedAt:yyyy-MM-dd HH:mm:ss},{EscapeCsvField(tx.ProductName)},{EscapeCsvField(tx.SKU)},{EscapeCsvField(tx.TransactionType)},{tx.QuantityChange},{tx.QuantityAfter},{EscapeCsvField(tx.Notes)},{EscapeCsvField(tx.CreatedBy)}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"inventory_adjustments_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/no-sales")]
    public async Task<IActionResult> ExportNoSales(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetNoSalesProductsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Product Name,SKU,Category,Price,Quantity On Hand");
        foreach (var p in data)
        {
            csv.AppendLine($"{EscapeCsvField(p.ProductName)},{EscapeCsvField(p.SKU)},{EscapeCsvField(p.CategoryName)},{p.Price},{p.QuantityOnHand}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"no_sales_products_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/repeat-customers")]
    public async Task<IActionResult> ExportRepeatCustomers(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetRepeatCustomersReportAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Customer Name,Customer Email,Total Orders,Total Spent,Average Order Value,First Order Date,Last Order Date");
        foreach (var c in data)
        {
            csv.AppendLine($"{EscapeCsvField(c.CustomerName)},{EscapeCsvField(c.CustomerEmail)},{c.TotalOrders},{c.TotalSpent},{c.AverageOrderValue},{c.FirstOrderDate:yyyy-MM-dd},{c.LastOrderDate:yyyy-MM-dd}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"repeat_customers_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/reports/export/failures-refunds")]
    public async Task<IActionResult> ExportFailuresRefunds(string? filter, DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var data = await admin.GetPaymentFailuresRefundsAsync(token, filter, start, end, ct);
        if (data is null) return BadRequest("Failed to retrieve report data for export.");

        var csv = new StringBuilder();
        csv.AppendLine("Order Number,Amount,Order Status,Payment Status,Payment Method,Date,Gateway,Failure Reason");
        foreach (var r in data)
        {
            csv.AppendLine($"{EscapeCsvField(r.OrderNumber)},{r.TotalAmount},{EscapeCsvField(r.OrderStatus)},{EscapeCsvField(r.PaymentStatus)},{EscapeCsvField(r.PaymentMethod)},{r.CreatedAt:yyyy-MM-dd HH:mm:ss},{EscapeCsvField(r.GatewayName)},{EscapeCsvField(r.FailureReason)}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"failures_refunds_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string EscapeCsvField(string? value)
    {
        if (value is null) return "";
        // Prevent formula injection
        if (value.StartsWith("=") || value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("@"))
        {
            value = "'" + value;
        }
        if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
