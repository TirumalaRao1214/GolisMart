using System.ComponentModel.DataAnnotations;
using GOLISMart.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Models;

// ── Admin Delivery ViewModels ─────────────────────────────────────────────────

public class AdminDeliveryListViewModel
{
    public PagedResult<AdminDeliveryOrderListDto>? Orders { get; init; }
    public IReadOnlyList<DeliveryPersonnelDto> Personnel { get; init; } = [];
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Pincode { get; init; }
    public int? PersonnelId { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminDeliveryDetailViewModel
{
    public AdminDeliveryOrderDetailDto? Order { get; init; }
    public IReadOnlyList<DeliveryPersonnelDto> Personnel { get; init; } = [];
    public bool ApiAvailable { get; init; } = true;

    [Display(Name = "Delivery Person")]
    public int? AssignPersonnelId { get; set; }

    [MaxLength(300)]
    [Display(Name = "Notes")]
    public string? AssignNotes { get; set; }

    [Required(ErrorMessage = "Unassign reason is required.")]
    [MaxLength(300)]
    [Display(Name = "Reason")]
    public string? UnassignReason { get; set; }
}

public class AdminPersonnelListViewModel
{
    public PagedResult<DeliveryPersonnelDto>? Personnel { get; init; }
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public bool ApiAvailable { get; init; } = true;
}

public class AdminPersonnelFormViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(200)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact number is required.")]
    [MaxLength(20)]
    [Display(Name = "Contact Number")]
    public string ContactNumber { get; set; } = string.Empty;

    [MaxLength(450)]
    [Display(Name = "Application User ID (optional)")]
    public string? UserId { get; set; }

    [MaxLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public int? PersonnelId { get; set; }
    public string? ErrorMessage { get; set; }
}

// ── Delivery Personnel Dashboard ViewModels ───────────────────────────────────

public class DeliveryDashboardViewModel
{
    public IReadOnlyList<DeliveryAssignmentDto> Assignments { get; init; } = [];
    public string? StatusFilter { get; init; }
    public bool ApiAvailable { get; init; } = true;
}

public class DeliveryDetailViewModel
{
    public DeliveryAssignmentDto? Assignment { get; init; }
    public bool ApiAvailable { get; init; } = true;

    [MaxLength(500)]
    [Display(Name = "Notes")]
    public string? StatusNotes { get; set; }

    public bool DeliveredConfirmed { get; set; }
}

// ── Customer Tracking ViewModels ──────────────────────────────────────────────

public class OrderTrackingViewModel
{
    public DeliveryTrackingDto? Tracking { get; init; }
    public OrderDetailDto? OrderDetail { get; init; }
    public bool ApiAvailable { get; init; } = true;
}
