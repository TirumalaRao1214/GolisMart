using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GOLISMart.Web.Controllers.Admin;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GOLISMart.Web.Tests;

public class ReportsControllerTests
{
    private readonly Mock<IAdminApiClient> _api = new();

    private AdminReportsController CreateController() =>
        new(_api.Object, NullLogger<AdminReportsController>.Instance);

    [Fact]
    public async Task Index_ReturnsViewWithModel_WhenApiSucceeds()
    {
        // Arrange
        var initial = new AnalyticsDashboardDto(
            new ReportFilterDto(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "last30"),
            100m, 100m, 0m, 0m,
            1, 1, 0, 0, 100m,
            1, 100m, 0, 0m,
            0, 0, 1, 0,
            new List<DailySalesTrendDto>(),
            new List<CategorySalesDto>(),
            new List<PaymentMethodDistributionDto>(),
            new List<ProductSalesPerformanceDto>()
        );

        _api.Setup(a => a.GetReportsDashboardAsync(It.IsAny<string>(), "last30", null, null, default))
            .ReturnsAsync(initial);

        var controller = CreateController();

        // Act
        var result = await controller.Index(default);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("~/Views/Admin/Reports/Index.cshtml");
        viewResult.Model.Should().BeEquivalentTo(initial);
    }

    [Fact]
    public async Task ExportOrders_EscapesFormulaInjection_ReturnsFileResult()
    {
        // Arrange
        var orders = new List<OrderReportDto>
        {
            // Untrusted customer name starting with "=" to trigger formula injection escaping
            new(1, "ORD1001", "=SUM(A1:A5)", "hacked@example.com", DateTime.UtcNow, "Delivered", "COD", "Paid", 150m)
        };

        _api.Setup(a => a.GetOrdersReportAsync(It.IsAny<string>(), null, null, null, null, default))
            .ReturnsAsync(orders);

        var controller = CreateController();

        // Act
        var result = await controller.ExportOrders(null, null, null, null, default);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().StartWith("orders_report_");

        var csvContent = Encoding.UTF8.GetString(fileResult.FileContents);
        // The "=" must be escaped with a single quote "'"
        csvContent.Should().Contain("'=SUM(A1:A5)");
    }
}
