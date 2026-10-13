using System.ComponentModel.DataAnnotations;
using GOLISMart.Web.Services;

namespace GOLISMart.Web.Models;

public class HomeViewModel
{
    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];
    public IReadOnlyList<ProductSummaryDto> FeaturedProducts { get; init; } = [];
    public IReadOnlyList<ProductSummaryDto> FreshVegetables { get; init; } = [];
    public IReadOnlyList<ProductSummaryDto> FreshFruits { get; init; } = [];
    public IReadOnlyList<ProductSummaryDto> DailyEssentials { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;
}

public class CategoryListViewModel
{
    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;
}

public class ProductListViewModel
{
    public PagedResult<ProductSummaryDto>? Products { get; init; }
    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];
    public string? SelectedCategory { get; init; }
    public string? SearchQuery { get; init; }
    public string SortBy { get; init; } = "name";
    public string SortOrder { get; init; } = "asc";
    public bool? InStock { get; init; }
    public bool ApiAvailable { get; init; } = true;
}

public class ProductDetailViewModel
{
    public ProductDetailDto? Product { get; init; }
    public IReadOnlyList<ProductSummaryDto> RelatedProducts { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;
    public bool NotFound { get; init; }
}

public class SearchViewModel
{
    public PagedResult<ProductSummaryDto>? Results { get; init; }
    public string? Query { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

// ── Account ViewModels ────────────────────────────────────────────────────────

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(200, ErrorMessage = "Full name must not exceed 200 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [Display(Name = "Mobile Number")]
    public string Mobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "I accept the terms and privacy policy")]
    public bool AcceptTerms { get; set; }

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AccountOverviewViewModel
{
    public CustomerProfileDto? Profile { get; init; }
    public bool ApiAvailable { get; init; } = true;
}

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(200, ErrorMessage = "Full name must not exceed 200 characters.")]
    [Display(Name = "Full Name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [Display(Name = "Mobile Number")]
    public string Mobile { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AddressListViewModel
{
    public IReadOnlyList<AddressDto> Addresses { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AddressFormViewModel
{
    [Required(ErrorMessage = "Recipient name is required.")]
    [MaxLength(200)]
    [Display(Name = "Recipient Name")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [Display(Name = "Mobile Number")]
    public string RecipientMobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address label is required.")]
    [Display(Name = "Address Label")]
    public string AddressLabel { get; set; } = "Home";

    [Required(ErrorMessage = "House/flat number is required.")]
    [MaxLength(100)]
    [Display(Name = "House / Flat No.")]
    public string HouseNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Building/street is required.")]
    [MaxLength(200)]
    [Display(Name = "Building / Street")]
    public string BuildingStreet { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area/locality is required.")]
    [MaxLength(200)]
    [Display(Name = "Area / Locality")]
    public string Locality { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Landmark (optional)")]
    public string? Landmark { get; set; }

    [Required(ErrorMessage = "City is required.")]
    [MaxLength(100)]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required.")]
    [MaxLength(100)]
    [Display(Name = "State")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN code must be exactly 6 digits.")]
    [Display(Name = "PIN Code")]
    public string Pincode { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Delivery Instructions (optional)")]
    public string? DeliveryInstructions { get; set; }

    [Display(Name = "Set as default address")]
    public bool IsDefault { get; set; }

    // For edit mode
    public int? AddressId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CartViewModel
{
    public CartDto? Cart { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal FreeDeliveryAbove { get; init; }
    public decimal Total => (Cart?.SubTotal ?? 0) + DeliveryFee;
    public bool IsFreeDelivery => (Cart?.SubTotal ?? 0) >= FreeDeliveryAbove;
    public decimal ActualDeliveryFee => IsFreeDelivery ? 0m : DeliveryFee;
    public decimal FinalTotal => (Cart?.SubTotal ?? 0) + ActualDeliveryFee;
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

// ── Checkout ViewModels ───────────────────────────────────────────────────────

public class CheckoutAddressViewModel
{
    public IReadOnlyList<AddressDto> Addresses { get; init; } = [];
    public CartDto? Cart { get; init; }
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public int? PreselectedAddressId { get; init; }
}

public class CheckoutReviewViewModel
{
    public CheckoutSummaryDto? Summary { get; init; }
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
    /// <summary>Issues from cart/delivery validation that blocked checkout.</summary>
    public IReadOnlyList<string> ValidationIssues { get; init; } = [];
}

// ── Order ViewModels ──────────────────────────────────────────────────────────

public class OrderConfirmationViewModel
{
    public OrderDetailDto? Order { get; init; }
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

public class MyOrdersViewModel
{
    public OrdersPagedResponse? PagedOrders { get; init; }
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public string? StatusFilter { get; init; }
    public string? DateFrom { get; init; }
    public string? DateTo { get; init; }
    public int CurrentPage { get; init; } = 1;
}

public class OrderDetailViewModel
{
    public OrderDetailDto? Order { get; init; }
    public DeliveryTrackingDto? Tracking { get; init; }
    public bool ApiAvailable { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }
}

// ── Admin ViewModels ──────────────────────────────────────────────────────────

public class AdminDashboardViewModel
{
    public DashboardSummaryDto? Summary { get; init; }
    public bool ApiAvailable { get; init; } = true;
}

public class AdminProductListViewModel
{
    public PagedResult<AdminProductDto>? Products { get; init; }
    public IReadOnlyList<AdminCategoryDto> Categories { get; init; } = [];
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminProductFormViewModel
{
    [Required(ErrorMessage = "Product name is required.")]
    [MaxLength(200)]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU is required.")]
    [MaxLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Unit code is required.")]
    [MaxLength(20)]
    [Display(Name = "Unit Code")]
    public string UnitCode { get; set; } = "KG";

    [Required(ErrorMessage = "Unit label is required.")]
    [MaxLength(50)]
    [Display(Name = "Unit Label")]
    public string UnitLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required.")]
    [Range(0.01, 999999)]
    [Display(Name = "Price (₹)")]
    public decimal Price { get; set; }

    [Range(0, 999999)]
    [Display(Name = "Offer Price (₹)")]
    public decimal? OfferPrice { get; set; }

    [Display(Name = "Featured")]
    public bool IsFeatured { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Range(0, 999999)]
    [Display(Name = "Initial Stock / Quantity on Hand")]
    public decimal InitialStock { get; set; }

    [Range(0, 9999)]
    [Display(Name = "Low Stock Threshold")]
    public decimal LowStockThreshold { get; set; } = 5;

    // For edit mode
    public int? ProductId { get; set; }
    public string? ExistingImagePath { get; set; }

    public IReadOnlyList<AdminCategoryDto> Categories { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class AdminCategoryListViewModel
{
    public PagedResult<AdminCategoryDto>? Categories { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminCategoryFormViewModel
{
    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required.")]
    [MaxLength(120)]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(0, 9999)]
    [Display(Name = "Sort Order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // For edit mode
    public int? CategoryId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminInventoryViewModel
{
    public PagedResult<AdminInventoryItemDto>? Inventory { get; init; }
    public string? Search { get; init; }
    public bool? LowStock { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminInventoryAdjustViewModel
{
    [Required]
    [Display(Name = "Product ID")]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quantity change is required.")]
    [Display(Name = "Quantity Change")]
    public decimal QuantityChange { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(200)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public class AdminOrderListViewModel
{
    public PagedResult<AdminOrderListDto>? Orders { get; init; }
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? PaymentStatus { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminOrderDetailViewModel
{
    public AdminOrderDetailDto? Order { get; init; }
    public bool ApiAvailable { get; init; } = true;

    [MaxLength(200)]
    [Display(Name = "Notes")]
    public string? StatusNotes { get; set; }

    [Required(ErrorMessage = "Please select a new status.")]
    [Display(Name = "New Status")]
    public string? NewStatus { get; set; }
}

public class AdminSettingsViewModel
{
    public IReadOnlyList<StoreSettingDto> Settings { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;
    /// <summary>Key → new value, populated from form POST.</summary>
    public Dictionary<string, string> Values { get; set; } = [];
}
