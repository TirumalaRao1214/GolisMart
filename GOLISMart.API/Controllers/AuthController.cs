using FluentValidation;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService auth,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        ILogger<AuthController> logger)
    {
        _auth              = auth;
        _registerValidator = registerValidator;
        _loginValidator    = loginValidator;
        _logger            = logger;
    }

    /// <summary>Register a new customer account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct = default)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error, response) = await _auth.RegisterAsync(request, ct);
        if (!success)
        {
            if (error!.Contains("already exists"))
                return Conflict(new ErrorResponse(error));
            return BadRequest(new ErrorResponse(error));
        }

        return Created($"/api/v1/auth/me", response);
    }

    /// <summary>Log in and receive a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error, response) = await _auth.LoginAsync(request, ct);
        if (!success)
            return Unauthorized(new ErrorResponse(error!));

        return Ok(response);
    }

    /// <summary>Get the authenticated customer's identity.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();

        var me = await _auth.GetMeAsync(userId, ct);
        if (me is null) return Unauthorized();

        return Ok(me);
    }

    /// <summary>Get roles of the authenticated user.</summary>
    [HttpGet("me-roles")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyRoles(CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Unauthorized();
        var roles = await _auth.GetUserRolesAsync(userId, ct);
        return Ok(roles);
    }
}

// Extension to convert FluentValidation failures to ASP.NET Core ValidationProblemDetails
internal static class ValidationExtensions
{
    public static ValidationProblemDetails ToValidationProblemDetails(
        this FluentValidation.Results.ValidationResult result)
    {
        var details = new ValidationProblemDetails();
        foreach (var failure in result.Errors)
        {
            var key = failure.PropertyName;
            if (!details.Errors.ContainsKey(key))
                details.Errors[key] = [];
            details.Errors[key] = [.. details.Errors[key], failure.ErrorMessage];
        }
        return details;
    }
}
