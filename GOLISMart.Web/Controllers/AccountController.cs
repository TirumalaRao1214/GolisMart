using System.Security.Claims;
using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthApiClient _auth;
    private readonly ILogger<AccountController> _logger;

    // Cookie name used to store the JWT (HttpOnly, not accessible to JS)
    private const string TokenCookieName = "GOLISMart_AuthToken";

    public AccountController(IAuthApiClient auth, ILogger<AccountController> logger)
    {
        _auth   = auth;
        _logger = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? GetStoredToken() =>
        HttpContext.Request.Cookies[TokenCookieName];

    private bool IsAjaxRequest() =>
        HttpContext != null && (
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("application/json") == true) ||
            Request.Headers["X-SPA-Request"] == "true"
        );

    private void StoreToken(AuthResponse authResponse)
    {
        var options = new CookieOptions
        {
            HttpOnly  = true,
            Secure    = Request.IsHttps,
            SameSite  = SameSiteMode.Lax,
            Expires   = authResponse.ExpiresAt
        };
        Response.Cookies.Append(TokenCookieName, authResponse.Token, options);
    }

    private void ClearToken() =>
        Response.Cookies.Delete(TokenCookieName);

    private async Task<IReadOnlyList<string>> SignInWithCookieAsync(AuthResponse authResponse)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authResponse.UserId),
            new(ClaimTypes.Email,          authResponse.Email),
            new(ClaimTypes.Name,           authResponse.DisplayName)
        };

        // Load roles and add them as role claims so [Authorize(Policy = "Admin")] works
        var roles = await _auth.GetUserRolesAsync(authResponse.Token);
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = authResponse.ExpiresAt
            });

        StoreToken(authResponse);
        return roles;
    }

    private IActionResult RedirectToSafe(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        if (User.IsInRole("Admin"))
            return RedirectToAction("Dashboard", "AdminDashboard");
        return RedirectToAction("Index", "Home");
    }

    // ── Registration ──────────────────────────────────────────────────────────

    [HttpGet("account/register")]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToSafe(returnUrl);
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("account/register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                var errors = ModelState.Where(ms => ms.Value?.Errors.Count > 0)
                    .ToDictionary(k => k.Key, v => v.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { success = false, message = "Please fix the validation errors.", errors });
            }
            return View(model);
        }

        if (!model.AcceptTerms)
        {
            ModelState.AddModelError(nameof(model.AcceptTerms), "You must accept the terms and privacy policy.");
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "You must accept the terms and privacy policy." });
            return View(model);
        }

        var request = new RegisterRequest(
            model.FullName, model.Email, model.Mobile,
            model.Password, model.ConfirmPassword, model.AcceptTerms);

        var (success, error, response) = await _auth.RegisterAsync(request, ct);

        if (!success)
        {
            if (error?.Contains("already exists") == true)
                ModelState.AddModelError(nameof(model.Email), error);
            else
                model.ErrorMessage = error ?? "Registration failed. Please try again.";

            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = error ?? "Registration failed. Please try again." });

            return View(model);
        }

        var roles = await SignInWithCookieAsync(response!);
        TempData["SuccessMessage"] = "Welcome to GOLIS Mart! Your account has been created.";

        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        string targetUrl;
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            targetUrl = model.ReturnUrl;
        }
        else if (isAdmin)
        {
            targetUrl = Url.Action("Dashboard", "AdminDashboard") ?? "/admin/dashboard";
        }
        else
        {
            targetUrl = Url.Action("Overview", "Account") ?? "/account";
        }

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Welcome to GOLIS Mart! Your account has been created.",
                redirectUrl = targetUrl,
                isAdmin = isAdmin,
                user = new { displayName = response?.DisplayName, email = response?.Email }
            });
        }

        return Redirect(targetUrl);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [HttpGet("account/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "AdminDashboard");
            return RedirectToAction("Index", "Home");
        }
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("account/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Please check your inputs and try again." });
            return View(model);
        }

        var (success, error, response) = await _auth.LoginAsync(
            new LoginRequest(model.Email, model.Password), ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Invalid email or password. Please try again.";
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = model.ErrorMessage });
            return View(model);
        }

        var roles = await SignInWithCookieAsync(response!);
        var isAdmin = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

        string targetUrl;
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            targetUrl = model.ReturnUrl;
        }
        else if (isAdmin)
        {
            targetUrl = Url.Action("Dashboard", "AdminDashboard") ?? "/admin/dashboard";
        }
        else
        {
            targetUrl = Url.Action("Index", "Home") ?? "/";
        }

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Signed in successfully!",
                redirectUrl = targetUrl,
                isAdmin = isAdmin,
                user = new { displayName = response?.DisplayName, email = response?.Email }
            });
        }

        return Redirect(targetUrl);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [HttpPost("account/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearToken();
        TempData["SuccessMessage"] = "You have been signed out.";
        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "You have been signed out.",
                redirectUrl = Url.Action("Index", "Home") ?? "/"
            });
        }
        return RedirectToAction("Index", "Home");
    }

    // ── Account Overview ──────────────────────────────────────────────────────

    [HttpGet("account")]
    [Authorize]
    public async Task<IActionResult> Overview(CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null)
            return RedirectToAction(nameof(Login));

        var profile = await _auth.GetProfileAsync(token, ct);
        return View(new AccountOverviewViewModel
        {
            Profile      = profile,
            ApiAvailable = profile is not null
        });
    }

    // ── Edit Profile ──────────────────────────────────────────────────────────

    [HttpGet("account/profile")]
    [Authorize]
    public async Task<IActionResult> EditProfile(CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        var profile = await _auth.GetProfileAsync(token, ct);
        if (profile is null) return RedirectToAction(nameof(Login));

        return View(new EditProfileViewModel
        {
            DisplayName = profile.DisplayName,
            Mobile      = profile.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost("account/profile")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(EditProfileViewModel model, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Please check your inputs." });
            return View(model);
        }

        var (success, error, profile) = await _auth.UpdateProfileAsync(
            token, new UpdateProfileRequest(model.DisplayName, model.Mobile), ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Could not update profile. Please try again.";
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = model.ErrorMessage });
            return View(model);
        }

        // Refresh ClaimsPrincipal with updated display name
        var identity = (ClaimsIdentity)User.Identity!;
        var nameClaim = identity.FindFirst(ClaimTypes.Name);
        if (nameClaim is not null) identity.RemoveClaim(nameClaim);
        identity.AddClaim(new Claim(ClaimTypes.Name, profile!.DisplayName));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            User,
            new AuthenticationProperties { IsPersistent = true });

        TempData["SuccessMessage"] = "Profile updated successfully.";

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Profile updated successfully.",
                redirectUrl = Url.Action("Overview", "Account")
            });
        }

        return RedirectToAction(nameof(Overview));
    }

    // ── Change Password ───────────────────────────────────────────────────────

    [HttpGet("account/change-password")]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost("account/change-password")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Please check your password inputs." });
            return View(model);
        }

        var (success, error) = await _auth.ChangePasswordAsync(
            token,
            new ChangePasswordRequest(model.CurrentPassword, model.NewPassword, model.ConfirmNewPassword),
            ct);

        if (!success)
        {
            var msg = error == "Current password is incorrect."
                ? error
                : "Password change failed. Please try again.";
            model.ErrorMessage = msg;
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = msg });
            return View(model);
        }

        // Sign out so user must re-login with new password (token invalidated by security stamp change)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ClearToken();
        TempData["SuccessMessage"] = "Password changed successfully. Please sign in with your new password.";

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Password changed successfully. Please sign in with your new password.",
                redirectUrl = Url.Action("Login", "Account")
            });
        }

        return RedirectToAction(nameof(Login));
    }

    // ── Addresses ─────────────────────────────────────────────────────────────

    [HttpGet("account/addresses")]
    [Authorize]
    public async Task<IActionResult> Addresses(CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        var addresses = await _auth.GetAddressesAsync(token, ct);
        return View(new AddressListViewModel { Addresses = addresses });
    }

    [HttpGet("account/addresses/add")]
    [HttpGet("account/addresses/new")]
    [Authorize]
    public IActionResult AddAddress() => View("AddressForm", new AddressFormViewModel());

    [HttpPost("account/addresses/add")]
    [HttpPost("account/addresses/new")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(AddressFormViewModel model, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Please check required address fields." });
            return View("AddressForm", model);
        }

        var request = new CreateAddressRequest(
            model.RecipientName, model.RecipientMobile, model.AddressLabel,
            model.HouseNumber, model.BuildingStreet, model.Locality, model.Landmark,
            model.City, model.State, model.Pincode, model.DeliveryInstructions, model.IsDefault);

        var (success, error, address) = await _auth.CreateAddressAsync(token, request, ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Could not save address. Please try again.";
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = model.ErrorMessage });
            return View("AddressForm", model);
        }

        TempData["SuccessMessage"] = "Address added successfully.";

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Address added successfully.",
                redirectUrl = Url.Action("Addresses", "Account")
            });
        }

        return RedirectToAction(nameof(Addresses));
    }

    [HttpGet("account/addresses/{id:int}/edit")]
    [Authorize]
    public async Task<IActionResult> EditAddress(int id, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        var addresses = await _auth.GetAddressesAsync(token, ct);
        var addr = addresses.FirstOrDefault(a => a.Id == id);
        if (addr is null) return NotFound();

        return View("AddressForm", new AddressFormViewModel
        {
            AddressId            = id,
            RecipientName        = addr.RecipientName,
            RecipientMobile      = addr.RecipientMobile,
            AddressLabel         = addr.AddressLabel,
            HouseNumber          = addr.HouseNumber,
            BuildingStreet       = addr.BuildingStreet,
            Locality             = addr.Locality,
            Landmark             = addr.Landmark,
            City                 = addr.City,
            State                = addr.State,
            Pincode              = addr.Pincode,
            DeliveryInstructions = addr.DeliveryInstructions,
            IsDefault            = addr.IsDefault
        });
    }

    [HttpPost("account/addresses/{id:int}/edit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAddress(int id, AddressFormViewModel model, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null) return RedirectToAction(nameof(Login));

        model.AddressId = id;
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Please check required address fields." });
            return View("AddressForm", model);
        }

        var request = new UpdateAddressRequest(
            model.RecipientName, model.RecipientMobile, model.AddressLabel,
            model.HouseNumber, model.BuildingStreet, model.Locality, model.Landmark,
            model.City, model.State, model.Pincode, model.DeliveryInstructions, model.IsDefault);

        var (success, error, address) = await _auth.UpdateAddressAsync(token, id, request, ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Could not update address. Please try again.";
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = model.ErrorMessage });
            return View("AddressForm", model);
        }

        TempData["SuccessMessage"] = "Address updated successfully.";

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success = true,
                message = "Address updated successfully.",
                redirectUrl = Url.Action("Addresses", "Account")
            });
        }

        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost("account/addresses/{id:int}/delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null)
        {
            if (IsAjaxRequest()) return Unauthorized();
            return RedirectToAction(nameof(Login));
        }

        var (success, error) = await _auth.DeleteAddressAsync(token, id, ct);
        if (!success)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = error ?? "Could not delete address." });
            TempData["ErrorMessage"] = error ?? "Could not delete address.";
            return RedirectToAction(nameof(Addresses));
        }

        if (IsAjaxRequest())
            return Ok(new { success = true, message = "Address deleted." });

        TempData["SuccessMessage"] = "Address deleted.";
        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost("account/addresses/{id:int}/set-default")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(int id, CancellationToken ct = default)
    {
        var token = GetStoredToken();
        if (token is null)
        {
            if (IsAjaxRequest()) return Unauthorized();
            return RedirectToAction(nameof(Login));
        }

        var (success, error) = await _auth.SetDefaultAddressAsync(token, id, ct);
        if (!success)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = error ?? "Could not set default address." });
            TempData["ErrorMessage"] = error ?? "Could not set default address.";
            return RedirectToAction(nameof(Addresses));
        }

        if (IsAjaxRequest())
            return Ok(new { success = true, message = "Default address updated." });

        TempData["SuccessMessage"] = "Default address updated.";
        return RedirectToAction(nameof(Addresses));
    }
}
