using FluentValidation;
using GOLISMart.API.DTOs;

namespace GOLISMart.API.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Must(BeValidIndianMobile).WithMessage("Enter a valid 10-digit Indian mobile number.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.AcceptTerms)
            .Equal(true).WithMessage("You must accept the terms and privacy policy to register.");
    }

    private static bool BeValidIndianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var digits = mobile.Replace(" ", "").Replace("-", "");
        // Accept: 10 digits starting 6-9, or +91XXXXXXXXXX
        if (digits.StartsWith("+91") && digits.Length == 13)
            digits = digits[3..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits.Length == 10
               && digits[0] is >= '6' and <= '9'
               && digits.All(char.IsDigit);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Please enter a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.Mobile)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Must(BeValidIndianMobile).WithMessage("Enter a valid 10-digit Indian mobile number.");
    }

    private static bool BeValidIndianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var digits = mobile.Replace(" ", "").Replace("-", "");
        if (digits.StartsWith("+91") && digits.Length == 13)
            digits = digits[3..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits.Length == 10
               && digits[0] is >= '6' and <= '9'
               && digits.All(char.IsDigit);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current password.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Please confirm your new password.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}

public class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    private static readonly string[] ValidLabels = ["Home", "Work", "Other"];

    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Recipient name is required.")
            .MaximumLength(200).WithMessage("Recipient name must not exceed 200 characters.");

        RuleFor(x => x.RecipientMobile)
            .NotEmpty().WithMessage("Recipient mobile number is required.")
            .Must(BeValidIndianMobile).WithMessage("Enter a valid 10-digit Indian mobile number.");

        RuleFor(x => x.AddressLabel)
            .NotEmpty().WithMessage("Address label is required.")
            .Must(l => ValidLabels.Contains(l)).WithMessage("Address label must be Home, Work, or Other.");

        RuleFor(x => x.HouseNumber)
            .NotEmpty().WithMessage("House/flat number is required.")
            .MaximumLength(100).WithMessage("House/flat number must not exceed 100 characters.");

        RuleFor(x => x.BuildingStreet)
            .NotEmpty().WithMessage("Building/street is required.")
            .MaximumLength(200).WithMessage("Building/street must not exceed 200 characters.");

        RuleFor(x => x.Locality)
            .NotEmpty().WithMessage("Area/locality is required.")
            .MaximumLength(200).WithMessage("Area/locality must not exceed 200 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100).WithMessage("State must not exceed 100 characters.");

        RuleFor(x => x.Pincode)
            .NotEmpty().WithMessage("PIN code is required.")
            .Matches(@"^\d{6}$").WithMessage("PIN code must be exactly 6 digits.");

        RuleFor(x => x.Landmark)
            .MaximumLength(200).WithMessage("Landmark must not exceed 200 characters.")
            .When(x => x.Landmark is not null);

        RuleFor(x => x.DeliveryInstructions)
            .MaximumLength(500).WithMessage("Delivery instructions must not exceed 500 characters.")
            .When(x => x.DeliveryInstructions is not null);
    }

    private static bool BeValidIndianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var digits = mobile.Replace(" ", "").Replace("-", "");
        if (digits.StartsWith("+91") && digits.Length == 13)
            digits = digits[3..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits.Length == 10
               && digits[0] is >= '6' and <= '9'
               && digits.All(char.IsDigit);
    }
}

public class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    private static readonly string[] ValidLabels = ["Home", "Work", "Other"];

    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Recipient name is required.")
            .MaximumLength(200).WithMessage("Recipient name must not exceed 200 characters.");

        RuleFor(x => x.RecipientMobile)
            .NotEmpty().WithMessage("Recipient mobile number is required.")
            .Must(BeValidIndianMobile).WithMessage("Enter a valid 10-digit Indian mobile number.");

        RuleFor(x => x.AddressLabel)
            .NotEmpty().WithMessage("Address label is required.")
            .Must(l => ValidLabels.Contains(l)).WithMessage("Address label must be Home, Work, or Other.");

        RuleFor(x => x.HouseNumber)
            .NotEmpty().WithMessage("House/flat number is required.")
            .MaximumLength(100).WithMessage("House/flat number must not exceed 100 characters.");

        RuleFor(x => x.BuildingStreet)
            .NotEmpty().WithMessage("Building/street is required.")
            .MaximumLength(200).WithMessage("Building/street must not exceed 200 characters.");

        RuleFor(x => x.Locality)
            .NotEmpty().WithMessage("Area/locality is required.")
            .MaximumLength(200).WithMessage("Area/locality must not exceed 200 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.")
            .MaximumLength(100).WithMessage("State must not exceed 100 characters.");

        RuleFor(x => x.Pincode)
            .NotEmpty().WithMessage("PIN code is required.")
            .Matches(@"^\d{6}$").WithMessage("PIN code must be exactly 6 digits.");
    }

    private static bool BeValidIndianMobile(string mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return false;
        var digits = mobile.Replace(" ", "").Replace("-", "");
        if (digits.StartsWith("+91") && digits.Length == 13)
            digits = digits[3..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits.Length == 10
               && digits[0] is >= '6' and <= '9'
               && digits.All(char.IsDigit);
    }
}
