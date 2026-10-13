using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace GOLISMart.API.Services.Implementations;

public class DeliveryService : IDeliveryService
{
    private readonly IDeliveryRepository _repo;
    private readonly ILogger<DeliveryService> _logger;

    // Regex-like validation: 6 digits, not all zeros
    private static bool IsValidPinFormat(string pin) =>
        pin.Length == 6 && pin.All(char.IsDigit) && pin != "000000";

    public DeliveryService(IDeliveryRepository repo, ILogger<DeliveryService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<ServiceabilityResponse> CheckServiceabilityAsync(string pinCode, CancellationToken ct = default)
    {
        if (!IsValidPinFormat(pinCode))
        {
            return new ServiceabilityResponse(
                PinCode: pinCode,
                IsServiceable: false,
                AreaName: null,
                EstimatedDeliveryWindow: null,
                MinOrderValue: null,
                DeliveryFee: null,
                FreeDeliveryAbove: null,
                UnavailableReason: "Invalid PIN code format. A valid Indian PIN code is 6 digits."
            );
        }

        Models.DeliveryArea? area;
        try
        {
            area = await _repo.GetByPinCodeAsync(pinCode, ct);
        }
        catch (SqlException ex) when (ex.Number == 208) // 208 = Invalid object name (table does not exist)
        {
            _logger.LogWarning(
                "DeliveryAreas table not found (SQL error 208). " +
                "Run database migration 008_DeliveryServiceability.sql to enable delivery serviceability checks.");
            return new ServiceabilityResponse(
                PinCode: pinCode,
                IsServiceable: false,
                AreaName: null,
                EstimatedDeliveryWindow: null,
                MinOrderValue: null,
                DeliveryFee: null,
                FreeDeliveryAbove: null,
                UnavailableReason: "Delivery serviceability has not been configured yet. " +
                                   "Please contact support or run the database migration."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking serviceability for PIN {PinCode}", pinCode);
            throw; // Re-throw non-SQL-208 errors to be handled by the caller
        }

        if (area is null)
        {
            _logger.LogDebug("Serviceability check: PIN {PinCode} is not in configured areas", pinCode);
            return new ServiceabilityResponse(
                PinCode: pinCode,
                IsServiceable: false,
                AreaName: null,
                EstimatedDeliveryWindow: null,
                MinOrderValue: null,
                DeliveryFee: null,
                FreeDeliveryAbove: null,
                UnavailableReason: "Delivery is not available to this PIN code yet."
            );
        }

        return new ServiceabilityResponse(
            PinCode: pinCode,
            IsServiceable: true,
            AreaName: area.AreaName,
            EstimatedDeliveryWindow: area.EstimatedDeliveryWindow,
            MinOrderValue: area.MinOrderValue,
            DeliveryFee: area.DeliveryFee,
            FreeDeliveryAbove: area.FreeDeliveryAbove,
            UnavailableReason: null
        );
    }

    public async Task<IReadOnlyList<DeliveryAreaDto>> GetActiveAreasAsync(CancellationToken ct = default)
    {
        try
        {
            var areas = await _repo.GetAllActiveAsync(ct);
            return areas.Select(a => new DeliveryAreaDto(
                a.Id,
                a.PinCode,
                a.AreaName,
                a.IsActive,
                a.EstimatedDeliveryWindow,
                a.MinOrderValue,
                a.DeliveryFee,
                a.FreeDeliveryAbove
            )).ToList();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning("DeliveryAreas table not found. Run migration 008_DeliveryServiceability.sql.");
            return [];
        }
    }
}
