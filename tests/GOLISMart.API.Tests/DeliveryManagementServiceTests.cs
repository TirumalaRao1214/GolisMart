using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for DeliveryManagementService — no database required.
/// </summary>
public class DeliveryManagementServiceTests
{
    private readonly Mock<IDeliveryManagementRepository> _repo   = new();
    private readonly Mock<IAdminRepository>              _admin  = new();
    private readonly Mock<INotificationProvider>         _notif  = new();

    private DeliveryManagementService CreateService() =>
        new(_repo.Object, _admin.Object,
            new[] { _notif.Object },
            NullLogger<DeliveryManagementService>.Instance);

    // ── Personnel: deactivation guard ────────────────────────────────────────

    [Fact]
    public async Task UpdatePersonnel_BlocksDeactivate_WhenActiveAssignmentsExist()
    {
        _repo.Setup(r => r.GetActiveAssignmentCountAsync(5, default))
             .ReturnsAsync(3);

        var svc = CreateService();
        var (success, error) = await svc.UpdatePersonnelAsync(
            5,
            new UpdateDeliveryPersonnelRequest("Alice", "9999", null, null, false),
            "admin1");

        success.Should().BeFalse();
        error.Should().Contain("3 active delivery assignment");
    }

    [Fact]
    public async Task UpdatePersonnel_Succeeds_WhenNoActiveAssignments()
    {
        _repo.Setup(r => r.GetActiveAssignmentCountAsync(7, default))
             .ReturnsAsync(0);
        _repo.Setup(r => r.UpdatePersonnelAsync(7, It.IsAny<UpdateDeliveryPersonnelRequest>(), default))
             .ReturnsAsync(true);
        _admin.Setup(r => r.LogAdminActionAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
              .Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.UpdatePersonnelAsync(
            7,
            new UpdateDeliveryPersonnelRequest("Bob", "8888", null, null, false),
            "admin1");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    // ── My Assignments: unknown personnel ────────────────────────────────────

    [Fact]
    public async Task GetMyAssignments_ReturnsEmpty_WhenPersonnelNotFound()
    {
        _repo.Setup(r => r.GetPersonnelByUserIdAsync("unknownUser", default))
             .ReturnsAsync((Models.DeliveryPersonnel?)null);

        var svc = CreateService();
        var result = await svc.GetMyAssignmentsAsync("unknownUser", null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyAssignments_ReturnsEmpty_WhenPersonnelInactive()
    {
        _repo.Setup(r => r.GetPersonnelByUserIdAsync("inactiveUser", default))
             .ReturnsAsync(new Models.DeliveryPersonnel { Id = 10, UserId = "inactiveUser", IsActive = false });

        var svc = CreateService();
        var result = await svc.GetMyAssignmentsAsync("inactiveUser", null);

        result.Should().BeEmpty();
    }

    // ── UpdateDeliveryStatus: personnel required ──────────────────────────────

    [Fact]
    public async Task UpdateDeliveryStatus_Fails_WhenPersonnelNotFound()
    {
        _repo.Setup(r => r.GetPersonnelByUserIdAsync("badUser", default))
             .ReturnsAsync((Models.DeliveryPersonnel?)null);

        var svc = CreateService();
        var (success, error) = await svc.UpdateDeliveryStatusAsync(100, "badUser", "PickedUp", null);

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── Notification: idempotency ─────────────────────────────────────────────

    [Fact]
    public async Task FireNotifications_SkipsWhenAlreadyQueued()
    {
        _notif.Setup(n => n.Channel).Returns("InApp");
        // TryQueueNotification returns null → already exists
        _repo.Setup(r => r.TryQueueNotificationAsync(
                1, "Delivered", "user1", null, null, "InApp", default))
             .ReturnsAsync((long?)null);

        var svc = CreateService();
        await svc.FireNotificationsAsync(1, "ORD001", "Delivered", "user1", null, null);

        // SendAsync must NOT be called when idempotency key already exists
        _notif.Verify(n => n.SendAsync(
            It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FireNotifications_CallsSend_WhenNewNotification()
    {
        _notif.Setup(n => n.Channel).Returns("InApp");
        _repo.Setup(r => r.TryQueueNotificationAsync(
                1, "Delivered", "user1", null, null, "InApp", default))
             .ReturnsAsync(42L);
        _notif.Setup(n => n.SendAsync(42L, 1, "ORD001", "Delivered", "user1", null, null, default))
             .ReturnsAsync(true);

        var svc = CreateService();
        await svc.FireNotificationsAsync(1, "ORD001", "Delivered", "user1", null, null);

        _notif.Verify(n => n.SendAsync(
            42L, 1, "ORD001", "Delivered", "user1", null, null, default), Times.Once);
    }

    [Fact]
    public async Task FireNotifications_DoesNotThrow_WhenProviderFails()
    {
        _notif.Setup(n => n.Channel).Returns("InApp");
        _repo.Setup(r => r.TryQueueNotificationAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), default))
             .ReturnsAsync(99L);
        _notif.Setup(n => n.SendAsync(
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
             .ThrowsAsync(new Exception("Provider down"));

        var svc = CreateService();

        // Must NOT propagate
        await svc.Invoking(s => s.FireNotificationsAsync(1, "ORD001", "Delivered", "user1", null, null))
                 .Should().NotThrowAsync();
    }

    // ── Assignment: inactive personnel ────────────────────────────────────────

    [Fact]
    public async Task AssignOrder_DelegatesToRepo()
    {
        _repo.Setup(r => r.AssignOrderAsync(10, 3, "adminX", null, default))
             .ReturnsAsync((true, (string?)null));
        _admin.Setup(r => r.LogAdminActionAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
              .Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.AssignOrderAsync(10, 3, "adminX", null);

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task UnassignOrder_ReturnsError_WhenNoActiveAssignment()
    {
        _repo.Setup(r => r.UnassignOrderAsync(99, "adminX", "No longer needed", default))
             .ReturnsAsync((false, "No active assignment found for this order."));

        var svc = CreateService();
        var (success, error) = await svc.UnassignOrderAsync(99, "adminX", "No longer needed");

        success.Should().BeFalse();
        error.Should().Contain("No active assignment");
    }

    // ── In-app notifications ──────────────────────────────────────────────────

    [Fact]
    public async Task GetInAppNotifications_ReturnsFromRepo()
    {
        var expected = new List<InAppNotificationDto>
        {
            new(1, 10, "ORD-001", "Delivered", "Your order has been delivered.", false, DateTime.UtcNow)
        };
        _repo.Setup(r => r.GetInAppNotificationsAsync("user1", 20, default))
             .ReturnsAsync(expected.AsReadOnly());

        var svc = CreateService();
        var result = await svc.GetInAppNotificationsAsync("user1", 20);

        result.Should().HaveCount(1);
        result[0].EventType.Should().Be("Delivered");
    }
}
