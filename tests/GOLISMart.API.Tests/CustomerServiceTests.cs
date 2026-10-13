using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for CustomerService — no database required.
/// </summary>
public class CustomerServiceTests
{
    private readonly Mock<IUserRepository>              _userRepo = new();
    private readonly Mock<IPasswordHasher<ApplicationUser>> _hasher = new();

    private CustomerService CreateService() =>
        new(_userRepo.Object, _hasher.Object, NullLogger<CustomerService>.Instance);

    // ── GetProfile ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_ReturnsProfile_WhenUserExists()
    {
        var user = BuildUser("u1");
        _userRepo.Setup(r => r.GetByIdAsync("u1", default)).ReturnsAsync(user);

        var svc = CreateService();
        var result = await svc.GetProfileAsync("u1");

        result.Should().NotBeNull();
        result!.UserId.Should().Be("u1");
        result.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task GetProfile_ReturnsNull_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync("missing", default)).ReturnsAsync((ApplicationUser?)null);
        var svc = CreateService();
        var result = await svc.GetProfileAsync("missing");
        result.Should().BeNull();
    }

    // ── UpdateProfile ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_Succeeds_WithValidInput()
    {
        var user = BuildUser("u2");
        _userRepo.Setup(r => r.GetByIdAsync("u2", default)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<ApplicationUser>(), default))
                 .Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.UpdateProfileAsync("u2",
            new UpdateProfileRequest { DisplayName = "New Name", Mobile = "9876543210" });

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_Fails_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync("missing", default)).ReturnsAsync((ApplicationUser?)null);
        var svc = CreateService();
        var (success, error) = await svc.UpdateProfileAsync("missing",
            new UpdateProfileRequest { DisplayName = "Name", Mobile = "9876543210" });

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Succeeds_WithCorrectCurrentPassword()
    {
        var user = BuildUser("u3");
        _userRepo.Setup(r => r.GetByIdAsync("u3", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash!, "OldPass@1"))
               .Returns(PasswordVerificationResult.Success);
        _hasher.Setup(h => h.HashPassword(user, "NewPass@1")).Returns("new_hash");
        _userRepo.Setup(r => r.UpdatePasswordHashAsync("u3", "new_hash", It.IsAny<string>(), default))
                 .Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.ChangePasswordAsync("u3",
            new ChangePasswordRequest
            {
                CurrentPassword = "OldPass@1",
                NewPassword     = "NewPass@1",
                ConfirmNewPassword = "NewPass@1"
            });

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_Fails_WithWrongCurrentPassword()
    {
        var user = BuildUser("u4");
        _userRepo.Setup(r => r.GetByIdAsync("u4", default)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash!, "WrongPass@1"))
               .Returns(PasswordVerificationResult.Failed);

        var svc = CreateService();
        var (success, error) = await svc.ChangePasswordAsync("u4",
            new ChangePasswordRequest
            {
                CurrentPassword = "WrongPass@1",
                NewPassword     = "NewPass@1",
                ConfirmNewPassword = "NewPass@1"
            });

        success.Should().BeFalse();
        error.Should().Contain("incorrect");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplicationUser BuildUser(string id) => new()
    {
        Id           = id,
        Email        = $"{id}@test.com",
        DisplayName  = "Test User",
        PhoneNumber  = "+919876543210",
        PasswordHash = "hashed_pw",
        IsActive     = true,
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow
    };
}
