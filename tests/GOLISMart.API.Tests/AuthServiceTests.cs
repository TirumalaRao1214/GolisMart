using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for AuthService — no database required.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository>              _userRepo = new();
    private readonly Mock<IPasswordHasher<ApplicationUser>> _hasher = new();

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]  = "TestSecret_AtLeast32Chars_ForHMAC_SHA256!",
                ["Jwt:Issuer"]     = "TestIssuer",
                ["Jwt:Audience"]   = "TestAudience",
                ["Jwt:ExpiryHours"] = "8"
            })
            .Build();

    private AuthService CreateService() =>
        new(_userRepo.Object, _hasher.Object, BuildConfig(),
            NullLogger<AuthService>.Instance);

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ReturnsSuccess_WithValidRequest()
    {
        _userRepo.Setup(r => r.ExistsByNormalizedEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync(false);
        _hasher.Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .Returns("hashed_password");
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<ApplicationUser>(), default))
                 .ReturnsAsync("new-user-id");
        _userRepo.Setup(r => r.GetRolesAsync(It.IsAny<string>(), default))
                 .ReturnsAsync(Array.Empty<string>());

        var service = CreateService();
        var request = new RegisterRequest
        {
            FullName        = "Test User",
            Email           = "test@example.com",
            Mobile          = "9876543210",
            Password        = "Test@1234",
            ConfirmPassword = "Test@1234",
            AcceptTerms     = true
        };

        var (success, error, response) = await service.RegisterAsync(request);

        success.Should().BeTrue();
        error.Should().BeNull();
        response.Should().NotBeNull();
        response!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailExists()
    {
        _userRepo.Setup(r => r.ExistsByNormalizedEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync(true);

        var service = CreateService();
        var request = new RegisterRequest
        {
            FullName        = "Test User",
            Email           = "existing@example.com",
            Mobile          = "9876543210",
            Password        = "Test@1234",
            ConfirmPassword = "Test@1234",
            AcceptTerms     = true
        };

        var (success, error, response) = await service.RegisterAsync(request);

        success.Should().BeFalse();
        error.Should().Contain("already exists");
        response.Should().BeNull();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsSuccess_WithValidCredentials()
    {
        var user = BuildUser("user-1", "test@example.com");
        _userRepo.Setup(r => r.GetByNormalizedEmailAsync("TEST@EXAMPLE.COM", default))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash!, "Correct@123"))
               .Returns(PasswordVerificationResult.Success);
        _userRepo.Setup(r => r.ResetAccessFailedCountAsync(user.Id, default))
                 .Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.GetRolesAsync(user.Id, default))
                 .ReturnsAsync(Array.Empty<string>());

        var service = CreateService();
        var (success, error, response) = await service.LoginAsync(
            new LoginRequest { Email = "test@example.com", Password = "Correct@123" });

        success.Should().BeTrue();
        error.Should().BeNull();
        response!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Login_Fails_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByNormalizedEmailAsync(It.IsAny<string>(), default))
                 .ReturnsAsync((ApplicationUser?)null);
        _hasher.Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
               .Returns("dummy");

        var service = CreateService();
        var (success, error, _) = await service.LoginAsync(
            new LoginRequest { Email = "notfound@example.com", Password = "SomePass@1" });

        success.Should().BeFalse();
        error.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword()
    {
        var user = BuildUser("user-2", "user@example.com");
        _userRepo.Setup(r => r.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", default))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyHashedPassword(user, user.PasswordHash!, "WrongPass@1"))
               .Returns(PasswordVerificationResult.Failed);
        _userRepo.Setup(r => r.IncrementAccessFailedCountAsync(user.Id, default))
                 .Returns(Task.CompletedTask);

        var service = CreateService();
        var (success, error, _) = await service.LoginAsync(
            new LoginRequest { Email = "user@example.com", Password = "WrongPass@1" });

        success.Should().BeFalse();
        error.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Login_Fails_WhenAccountLocked()
    {
        var user = BuildUser("user-3", "locked@example.com");
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);

        _userRepo.Setup(r => r.GetByNormalizedEmailAsync("LOCKED@EXAMPLE.COM", default))
                 .ReturnsAsync(user);

        var service = CreateService();
        var (success, error, _) = await service.LoginAsync(
            new LoginRequest { Email = "locked@example.com", Password = "AnyPass@1" });

        success.Should().BeFalse();
        error.Should().Contain("locked");
    }

    // ── GetMe ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_ReturnsResponse_WhenUserExists()
    {
        var user = BuildUser("user-4", "me@example.com");
        _userRepo.Setup(r => r.GetByIdAsync("user-4", default)).ReturnsAsync(user);

        var service = CreateService();
        var result = await service.GetMeAsync("user-4");

        result.Should().NotBeNull();
        result!.UserId.Should().Be("user-4");
        result.Email.Should().Be("me@example.com");
    }

    [Fact]
    public async Task GetMe_ReturnsNull_WhenUserNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync("missing", default)).ReturnsAsync((ApplicationUser?)null);
        var service = CreateService();
        var result = await service.GetMeAsync("missing");
        result.Should().BeNull();
    }

    // ── MobileNormalization ───────────────────────────────────────────────────

    [Theory]
    [InlineData("9876543210",     "+919876543210")]
    [InlineData("+919876543210",  "+919876543210")]
    [InlineData("09876543210",    "+919876543210")]
    [InlineData("98765 43210",    "+919876543210")]
    public void NormalizeMobile_ProducesCorrectFormat(string input, string expected)
    {
        AuthService.NormalizeMobile(input).Should().Be(expected);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplicationUser BuildUser(string id, string email) => new()
    {
        Id            = id,
        Email         = email,
        NormalizedEmail = email.ToUpperInvariant(),
        DisplayName   = "Test User",
        PasswordHash  = "hashed",
        LockoutEnabled = true,
        LockoutEnd    = null,
        AccessFailedCount = 0,
        IsActive      = true,
        CreatedAt     = DateTime.UtcNow
    };
}
