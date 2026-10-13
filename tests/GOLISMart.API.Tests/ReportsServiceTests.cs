using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GOLISMart.API.Controllers.Admin;
using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GOLISMart.API.Tests;

public class ReportsServiceTests
{
    private readonly Mock<IAdminRepository> _repo = new();

    private AdminService CreateService() =>
        new(_repo.Object, null!, null!, NullLogger<AdminService>.Instance);

    [Fact]
    public async Task GetAnalyticsDashboardAsync_ReturnsCorrectData_WhenCalled()
    {
        // Arrange
        var service = CreateService();
        var start = DateTime.UtcNow.AddDays(-30);
        var end = DateTime.UtcNow;

        var expectedResult = new AnalyticsDashboardDto(
            new ReportFilterDto(start, end, "last30"),
            1000m, 950m, 100m, 50m,
            10, 8, 1, 1, 95m,
            5, 450m, 5, 500m,
            3, 1, 4, 6,
            new List<DailySalesTrendDto>(),
            new List<CategorySalesDto>(),
            new List<PaymentMethodDistributionDto>(),
            new List<ProductSalesPerformanceDto>()
        );

        _repo.Setup(r => r.GetAnalyticsDashboardAsync(start, end, "last30", default))
             .ReturnsAsync(expectedResult);

        // Act
        var result = await service.GetAnalyticsDashboardAsync(start, end, "last30", default);

        // Assert
        result.Should().NotBeNull();
        result.GrossSales.Should().Be(1000m);
        result.NetSales.Should().Be(950m);
        result.TotalOrders.Should().Be(10);
        result.CompletedOrders.Should().Be(8);
        result.AverageOrderValue.Should().Be(95m);
        result.NewCustomersCount.Should().Be(4);
        result.RepeatCustomersCount.Should().Be(6);
    }

    [Fact]
    public async Task GetNoSalesProductsAsync_ReturnsList_WhenNoSales()
    {
        // Arrange
        var service = CreateService();
        var start = DateTime.UtcNow.AddDays(-30);
        var end = DateTime.UtcNow;

        var expectedResult = new List<NoSalesProductDto>
        {
            new(1, "SKU1", "Unsold Product", "Grocery", 12.5m, 100)
        };

        _repo.Setup(r => r.GetNoSalesProductsAsync(start, end, default))
             .ReturnsAsync(expectedResult);

        // Act
        var result = await service.GetNoSalesProductsAsync(start, end, default);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].ProductName.Should().Be("Unsold Product");
        result[0].Price.Should().Be(12.5m);
    }

    [Fact]
    public async Task AdminReportsController_ReturnsBadRequest_WhenStartAfterEnd()
    {
        // Arrange
        var serviceMock = new Mock<IAdminService>();
        var controller = new AdminReportsController(serviceMock.Object);
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow;

        // Act
        var result = await controller.GetDashboard("custom", start, end, default);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminReportsController_ReturnsBadRequest_WhenRangeTooLarge()
    {
        // Arrange
        var serviceMock = new Mock<IAdminService>();
        var controller = new AdminReportsController(serviceMock.Object);
        var start = DateTime.UtcNow.AddDays(-400);
        var end = DateTime.UtcNow;

        // Act
        var result = await controller.GetDashboard("custom", start, end, default);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
