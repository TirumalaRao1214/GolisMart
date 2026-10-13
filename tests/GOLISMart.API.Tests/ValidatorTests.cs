using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Validators;

namespace GOLISMart.API.Tests;

/// <summary>
/// Validator tests for registration, login, profile update, and address forms.
/// </summary>
public class ValidatorTests
{
    // ── RegisterRequest ───────────────────────────────────────────────────────

    [Fact]
    public async Task Register_Valid_PassesValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName        = "Test User",
            Email           = "test@example.com",
            Mobile          = "9876543210",
            Password        = "Test@1234",
            ConfirmPassword = "Test@1234",
            AcceptTerms     = true
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Register_Fails_WhenEmailInvalid()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "not-an-email", Mobile = "9876543210",
            Password = "Test@1234", ConfirmPassword = "Test@1234", AcceptTerms = true
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task Register_Fails_WhenPasswordTooShort()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "t@t.com", Mobile = "9876543210",
            Password = "Sh@1", ConfirmPassword = "Sh@1", AcceptTerms = true
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Register_Fails_WhenPasswordMissingSpecialChar()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "t@t.com", Mobile = "9876543210",
            Password = "Test12345678", ConfirmPassword = "Test12345678", AcceptTerms = true
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task Register_Fails_WhenPasswordsMismatch()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "t@t.com", Mobile = "9876543210",
            Password = "Test@1234", ConfirmPassword = "Test@9999", AcceptTerms = true
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ConfirmPassword");
    }

    [Fact]
    public async Task Register_Fails_WhenTermsNotAccepted()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "t@t.com", Mobile = "9876543210",
            Password = "Test@1234", ConfirmPassword = "Test@1234", AcceptTerms = false
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AcceptTerms");
    }

    // ── Indian mobile validation ───────────────────────────────────────────────

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("+919876543210", true)]
    [InlineData("09876543210", true)]
    [InlineData("1234567890", false)]  // starts with 1 — invalid
    [InlineData("12345", false)]       // too short
    [InlineData("abcdefghij", false)]  // non-numeric
    public async Task Register_MobileValidation(string mobile, bool shouldPass)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest
        {
            FullName = "Test", Email = "t@t.com", Mobile = mobile,
            Password = "Test@1234", ConfirmPassword = "Test@1234", AcceptTerms = true
        };
        var result = await validator.ValidateAsync(request);
        var mobileErrors = result.Errors.Where(e => e.PropertyName == "Mobile").ToList();
        if (shouldPass)
            mobileErrors.Should().BeEmpty();
        else
            mobileErrors.Should().NotBeEmpty();
    }

    // ── AddressRequest ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAddress_Valid_PassesValidation()
    {
        var validator = new CreateAddressRequestValidator();
        var request = new CreateAddressRequest
        {
            RecipientName   = "Test",
            RecipientMobile = "9876543210",
            AddressLabel    = "Home",
            HouseNumber     = "12",
            BuildingStreet  = "Main St",
            Locality        = "Area",
            City            = "Mumbai",
            State           = "Maharashtra",
            Pincode         = "400001"
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345", false)]   // 5 digits — invalid
    [InlineData("1234567", false)] // 7 digits — invalid
    [InlineData("400001", true)]   // 6 digits — valid
    [InlineData("110001", true)]   // 6 digits — valid
    public async Task CreateAddress_PincodeValidation(string pincode, bool shouldPass)
    {
        var validator = new CreateAddressRequestValidator();
        var request = new CreateAddressRequest
        {
            RecipientName   = "Test",
            RecipientMobile = "9876543210",
            AddressLabel    = "Home",
            HouseNumber     = "12",
            BuildingStreet  = "Main St",
            Locality        = "Area",
            City            = "Mumbai",
            State           = "Maharashtra",
            Pincode         = pincode
        };
        var result = await validator.ValidateAsync(request);
        var errors = result.Errors.Where(e => e.PropertyName == "Pincode").ToList();
        if (shouldPass)
            errors.Should().BeEmpty();
        else
            errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAddress_Fails_WithInvalidLabel()
    {
        var validator = new CreateAddressRequestValidator();
        var request = new CreateAddressRequest
        {
            RecipientName   = "Test",
            RecipientMobile = "9876543210",
            AddressLabel    = "Office",  // invalid
            HouseNumber     = "12",
            BuildingStreet  = "Main St",
            Locality        = "Area",
            City            = "Mumbai",
            State           = "Maharashtra",
            Pincode         = "400001"
        };
        var result = await validator.ValidateAsync(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AddressLabel");
    }
}
