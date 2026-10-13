using System;
using System.Threading.Tasks;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/reports")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminReportsController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        if ((endDate - startDate).TotalDays > 366)
            return BadRequest(new { Error = "Custom reporting period cannot exceed 1 year." });

        var data = await _admin.GetAnalyticsDashboardAsync(startDate, endDate, filter ?? "last30", ct);
        return Ok(data);
    }

    [HttpGet("no-sales")]
    public async Task<IActionResult> GetNoSales(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        var data = await _admin.GetNoSalesProductsAsync(startDate, endDate, ct);
        return Ok(data);
    }

    [HttpGet("inventory-quantities")]
    public async Task<IActionResult> GetInventoryQuantities(CancellationToken ct)
    {
        var data = await _admin.GetInventoryQuantitiesReportAsync(ct);
        return Ok(data);
    }

    [HttpGet("inventory-adjustments")]
    public async Task<IActionResult> GetInventoryAdjustments(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        var data = await _admin.GetInventoryAdjustmentsAsync(startDate, endDate, ct);
        return Ok(data);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        var data = await _admin.GetOrdersReportAsync(startDate, endDate, status, ct);
        return Ok(data);
    }

    [HttpGet("failures-refunds")]
    public async Task<IActionResult> GetFailuresRefunds(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        var data = await _admin.GetPaymentFailuresRefundsAsync(startDate, endDate, ct);
        return Ok(data);
    }

    [HttpGet("repeat-customers")]
    public async Task<IActionResult> GetRepeatCustomers(
        [FromQuery] string? filter,
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken ct)
    {
        var (startDate, endDate) = ResolveDates(filter, start, end);
        if (startDate > endDate)
            return BadRequest(new { Error = "Start date must be before or equal to end date." });

        var data = await _admin.GetRepeatCustomersReportAsync(startDate, endDate, ct);
        return Ok(data);
    }

    private static (DateTime Start, DateTime End) ResolveDates(string? filter, DateTime? customStart, DateTime? customEnd)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        return filter?.ToLowerInvariant() switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "yesterday" => (today.AddDays(-1), today.AddTicks(-1)),
            "last7" => (today.AddDays(-6), today.AddDays(1).AddTicks(-1)),
            "last30" => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)),
            "month" => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1)),
            "prevmonth" => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1), new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1)),
            "custom" => (customStart ?? today.AddDays(-29), customEnd ?? today.AddDays(1).AddTicks(-1)),
            _ => (today.AddDays(-29), today.AddDays(1).AddTicks(-1)) // default to last 30 days
        };
    }
}
