using FluentValidation;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Produces("application/json")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly IAddressService _addresses;
    private readonly IValidator<UpdateProfileRequest> _profileValidator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;
    private readonly IValidator<CreateAddressRequest> _createAddressValidator;
    private readonly IValidator<UpdateAddressRequest> _updateAddressValidator;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customers,
        IAddressService addresses,
        IValidator<UpdateProfileRequest> profileValidator,
        IValidator<ChangePasswordRequest> passwordValidator,
        IValidator<CreateAddressRequest> createAddressValidator,
        IValidator<UpdateAddressRequest> updateAddressValidator,
        ILogger<CustomersController> logger)
    {
        _customers              = customers;
        _addresses              = addresses;
        _profileValidator       = profileValidator;
        _passwordValidator      = passwordValidator;
        _createAddressValidator = createAddressValidator;
        _updateAddressValidator = updateAddressValidator;
        _logger                 = logger;
    }

    private string? GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // ── Profile ───────────────────────────────────────────────────────────────

    /// <summary>Get the authenticated customer's profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var profile = await _customers.GetProfileAsync(userId, ct);
        if (profile is null) return NotFound();
        return Ok(profile);
    }

    /// <summary>Update name and mobile number.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _profileValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error) = await _customers.UpdateProfileAsync(userId, request, ct);
        if (!success) return BadRequest(new ErrorResponse(error!));

        var profile = await _customers.GetProfileAsync(userId, ct);
        return Ok(profile);
    }

    /// <summary>Change password (requires current password verification).</summary>
    [HttpPost("me/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _passwordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error) = await _customers.ChangePasswordAsync(userId, request, ct);
        if (!success) return BadRequest(new ErrorResponse(error!));
        return NoContent();
    }

    // ── Addresses ──────────────────────────────────────────────────────────────

    /// <summary>List all delivery addresses for the authenticated customer.</summary>
    [HttpGet("me/addresses")]
    [ProducesResponseType(typeof(IReadOnlyList<AddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAddresses(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var list = await _addresses.GetAddressesAsync(userId, ct);
        return Ok(list);
    }

    /// <summary>Add a new delivery address.</summary>
    [HttpPost("me/addresses")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateAddressRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createAddressValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error, address) = await _addresses.CreateAddressAsync(userId, request, ct);
        if (!success) return BadRequest(new ErrorResponse(error!));
        return CreatedAtAction(nameof(GetAddress), new { addressId = address!.Id }, address);
    }

    /// <summary>Get a specific delivery address.</summary>
    [HttpGet("me/addresses/{addressId:int}")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAddress(int addressId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var address = await _addresses.GetAddressAsync(addressId, userId, ct);
        if (address is null) return NotFound();
        return Ok(address);
    }

    /// <summary>Update a delivery address.</summary>
    [HttpPut("me/addresses/{addressId:int}")]
    [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAddress(
        int addressId, [FromBody] UpdateAddressRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var validation = await _updateAddressValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.ToValidationProblemDetails());

        var (success, error, address) = await _addresses.UpdateAddressAsync(addressId, userId, request, ct);
        if (!success)
        {
            if (error == "Address not found.") return NotFound();
            return BadRequest(new ErrorResponse(error!));
        }
        return Ok(address);
    }

    /// <summary>Soft-delete a delivery address.</summary>
    [HttpDelete("me/addresses/{addressId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(int addressId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var (success, error) = await _addresses.DeleteAddressAsync(addressId, userId, ct);
        if (!success)
        {
            if (error == "Address not found.") return NotFound();
            return BadRequest(new ErrorResponse(error!));
        }
        return NoContent();
    }

    /// <summary>Set an address as the default delivery address.</summary>
    [HttpPut("me/addresses/{addressId:int}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(int addressId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var (success, error) = await _addresses.SetDefaultAsync(addressId, userId, ct);
        if (!success)
        {
            if (error == "Address not found.") return NotFound();
            return BadRequest(new ErrorResponse(error!));
        }
        return NoContent();
    }
}
