using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

/// <summary>
/// Validates every aspect of a pending checkout:
///   1. Cart is not empty.
///   2. The address belongs to the authenticated customer.
///   3. The delivery PIN code is serviceable.
///   4. Every cart item: product is active, in stock, quantity valid, live price.
///   5. Delivery fee and free-delivery threshold come from serviceability data.
///   6. Minimum order value enforced when configured.
/// </summary>
public class CheckoutService : ICheckoutService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IAddressRepository _addressRepo;
    private readonly IDeliveryService _deliveryService;
    private readonly IConfiguration _config;
    private readonly ILogger<CheckoutService> _logger;

    // Fallback delivery settings from appsettings (CartSettings section)
    private decimal DefaultDeliveryFee => _config.GetValue<decimal>("CartSettings:DeliveryFee", 40m);
    private decimal DefaultFreeDeliveryAbove => _config.GetValue<decimal>("CartSettings:FreeDeliveryAbove", 500m);

    public CheckoutService(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        IAddressRepository addressRepo,
        IDeliveryService deliveryService,
        IConfiguration config,
        ILogger<CheckoutService> logger)
    {
        _cartRepo       = cartRepo;
        _productRepo    = productRepo;
        _inventoryRepo  = inventoryRepo;
        _addressRepo    = addressRepo;
        _deliveryService = deliveryService;
        _config         = config;
        _logger         = logger;
    }

    public async Task<CheckoutValidationResponse> ValidateAsync(
        string sessionKey,
        int addressId,
        string userId,
        CancellationToken ct = default)
    {
        var issues = new List<string>();

        try
        {
            return await ValidateInternalAsync(sessionKey, addressId, userId, issues, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Checkout validation threw an unhandled exception for user {UserId}, " +
                "session {SessionKey}, address {AddressId}",
                userId, sessionKey, addressId);

            // Surface as a validation failure with a safe message — never expose internal exception details
            return Fail(["A server error occurred while preparing your order. Please try again in a moment."]);
        }
    }

    private async Task<CheckoutValidationResponse> ValidateInternalAsync(
        string sessionKey,
        int addressId,
        string userId,
        List<string> issues,
        CancellationToken ct)
    {
        // ── 1. Load cart ──────────────────────────────────────────────────────
        var cart = await _cartRepo.GetBySessionKeyAsync(sessionKey, ct);
        if (cart is null)
        {
            issues.Add("Your cart is empty. Please add items before proceeding to checkout.");
            return Fail(issues);
        }

        var cartItems = await _cartRepo.GetItemsAsync(cart.Id, ct);
        if (cartItems.Count == 0)
        {
            issues.Add("Your cart is empty. Please add items before proceeding to checkout.");
            return Fail(issues);
        }

        // ── 2. Verify address ownership (NEVER trust addressId from client alone) ──
        var address = await _addressRepo.GetByIdAndUserIdAsync(addressId, userId, ct);
        if (address is null)
        {
            issues.Add("The selected delivery address was not found. Please choose a valid address.");
            return Fail(issues);
        }

        // ── 3. Check delivery serviceability ─────────────────────────────────
        var serviceability = await _deliveryService.CheckServiceabilityAsync(address.Pincode, ct);
        if (!serviceability.IsServiceable)
        {
            issues.Add(serviceability.UnavailableReason
                ?? "Delivery is not available to the selected address.");
            return Fail(issues);
        }

        // ── 4. Validate every cart item against live database state ───────────
        var productIds   = cartItems.Select(i => i.ProductId).Distinct().ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(productIds, ct);

        var lineItems = new List<CheckoutLineItem>();
        foreach (var ci in cartItems)
        {
            var product = await _productRepo.GetByIdAsync(ci.ProductId, ct);

            if (product is null || !product.IsActive)
            {
                issues.Add($"The product in your cart (ID {ci.ProductId}) is no longer available. Please remove it and try again.");
                continue;
            }

            inventoryMap.TryGetValue(ci.ProductId, out var inv);
            var isInStock = inv?.IsInStock ?? false;

            if (!isInStock)
            {
                issues.Add($"'{product.Name}' is currently out of stock. Please remove it from your cart.");
                continue;
            }

            if (ci.Quantity <= 0 || ci.Quantity > 20)
            {
                issues.Add($"'{product.Name}' has an invalid quantity. Please update your cart.");
                continue;
            }

            if (inv!.AvailableQuantity < ci.Quantity)
            {
                issues.Add($"Only {inv.AvailableQuantity} unit(s) of '{product.Name}' are available. " +
                           $"You have {ci.Quantity} in your cart. Please update the quantity.");
                continue;
            }

            var unitPrice     = product.OfferPrice ?? product.Price;
            var originalPrice = product.Price;
            var lineTotal     = Math.Round(unitPrice * ci.Quantity, 2);

            lineItems.Add(new CheckoutLineItem(
                ProductId:     product.Id,
                Name:          product.Name,
                UnitLabel:     product.UnitLabel,
                ImagePath:     product.ImagePath,
                Quantity:      ci.Quantity,
                UnitPrice:     unitPrice,
                OriginalPrice: originalPrice,
                LineTotal:     lineTotal,
                IsAvailable:   true,
                IsInStock:     true
            ));
        }

        // If any item issue was found, fail with validation issues (not a server error)
        if (issues.Count > 0)
            return Fail(issues);

        // ── 5. Compute totals ─────────────────────────────────────────────────
        var subTotal       = lineItems.Sum(l => l.LineTotal);
        var deliveryFee    = serviceability.DeliveryFee ?? DefaultDeliveryFee;
        var freeAbove      = serviceability.FreeDeliveryAbove ?? DefaultFreeDeliveryAbove;
        var isFreeDelivery = subTotal >= freeAbove;
        var actualDelivery = isFreeDelivery ? 0m : deliveryFee;
        var finalTotal     = subTotal + actualDelivery;

        // ── 6. Minimum order check ────────────────────────────────────────────
        var minOrder = serviceability.MinOrderValue;
        var meetsMin = minOrder is null || subTotal >= minOrder.Value;

        if (!meetsMin)
        {
            issues.Add($"A minimum order of ₹{minOrder:N2} is required for delivery to this area. " +
                       $"Your current subtotal is ₹{subTotal:N2}.");
            return Fail(issues);
        }

        _logger.LogInformation(
            "Checkout validated for user {UserId}: {ItemCount} item(s), subtotal ₹{SubTotal:N2}, delivery ₹{Delivery:N2}",
            userId, lineItems.Count, subTotal, actualDelivery);

        var checkoutAddress = new CheckoutAddressDto(
            AddressId:            address.Id,
            RecipientName:        address.RecipientName,
            RecipientMobile:      address.RecipientMobile,
            AddressLabel:         address.AddressLabel,
            HouseNumber:          address.HouseNumber,
            BuildingStreet:       address.BuildingStreet,
            Locality:             address.Locality,
            Landmark:             address.Landmark,
            City:                 address.City,
            State:                address.State,
            PinCode:              address.Pincode,
            DeliveryInstructions: address.DeliveryInstructions,
            IsDefault:            address.IsDefault
        );

        var summary = new CheckoutSummary(
            Address:                 checkoutAddress,
            Items:                   lineItems,
            SubTotal:                subTotal,
            DeliveryFee:             actualDelivery,
            FinalTotal:              finalTotal,
            IsFreeDelivery:          isFreeDelivery,
            FreeDeliveryAbove:       serviceability.FreeDeliveryAbove ?? DefaultFreeDeliveryAbove,
            MinOrderValue:           minOrder,
            MeetsMinOrder:           meetsMin,
            EstimatedDeliveryWindow: serviceability.EstimatedDeliveryWindow,
            PinCode:                 address.Pincode
        );

        return new CheckoutValidationResponse(IsValid: true, Issues: [], Summary: summary);
    }

    private static CheckoutValidationResponse Fail(List<string> issues) =>
        new(IsValid: false, Issues: issues, Summary: null);
}
