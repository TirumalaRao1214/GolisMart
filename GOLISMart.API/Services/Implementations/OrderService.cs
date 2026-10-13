using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

/// <summary>
/// Implements the order placement workflow:
///   1. Idempotency check (prevents duplicate submissions)
///   2. Validate cart and address ownership
///   3. Check delivery serviceability
///   4. Revalidate product prices, availability, and stock
///   5. Calculate server-side totals
///   6. Persist order + items + inventory deduction in a single transaction
///   7. Clear the cart
/// </summary>
public class OrderService : IOrderService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IAddressRepository _addressRepo;
    private readonly IDeliveryService _deliveryService;
    private readonly IOrderRepository _orderRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderService> _logger;

    private static readonly HashSet<string> AllowedPaymentMethods =
        new(StringComparer.OrdinalIgnoreCase) { "COD", "UPI", "Card", "NetBanking", "Wallet" };

    private decimal DefaultDeliveryFee =>
        _config.GetValue<decimal>("CartSettings:DeliveryFee", 40m);

    private decimal DefaultFreeDeliveryAbove =>
        _config.GetValue<decimal>("CartSettings:FreeDeliveryAbove", 500m);

    public OrderService(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        IAddressRepository addressRepo,
        IDeliveryService deliveryService,
        IOrderRepository orderRepo,
        IConfiguration config,
        ILogger<OrderService> logger)
    {
        _cartRepo       = cartRepo;
        _productRepo    = productRepo;
        _inventoryRepo  = inventoryRepo;
        _addressRepo    = addressRepo;
        _deliveryService = deliveryService;
        _orderRepo      = orderRepo;
        _config         = config;
        _logger         = logger;
    }

    // ── PlaceOrderAsync ────────────────────────────────────────────────────────

    public async Task<PlaceOrderResponse> PlaceOrderAsync(
        PlaceOrderRequest request,
        string userId,
        CancellationToken ct = default)
    {
        // ── 0. Idempotency check ───────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _orderRepo.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, userId, ct);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Duplicate order submission detected for user {UserId}, idempotency key {Key}. " +
                    "Returning existing order {OrderNumber}.",
                    userId, request.IdempotencyKey, existing.OrderNumber);

                return await BuildConfirmationFromOrder(existing, ct);
            }
        }

        // ── 1. Payment method allowlist ────────────────────────────────────────
        var paymentMethod = request.PaymentMethod?.Trim() ?? "COD";
        if (!AllowedPaymentMethods.Contains(paymentMethod))
            throw new ArgumentException($"Payment method '{paymentMethod}' is not supported.");

        // ── 2. Load and validate cart ─────────────────────────────────────────
        var cart = await _cartRepo.GetBySessionKeyAsync(request.SessionKey, ct)
            ?? throw new InvalidOperationException("Your cart is empty. Please add items before placing an order.");

        var cartItems = await _cartRepo.GetItemsAsync(cart.Id, ct);
        if (cartItems.Count == 0)
            throw new InvalidOperationException("Your cart is empty. Please add items before placing an order.");

        // ── 3. Verify address ownership ───────────────────────────────────────
        var address = await _addressRepo.GetByIdAndUserIdAsync(request.AddressId, userId, ct)
            ?? throw new ArgumentException("The selected delivery address was not found.");

        // ── 4. Check delivery serviceability ──────────────────────────────────
        var serviceability = await _deliveryService.CheckServiceabilityAsync(address.Pincode, ct);
        if (!serviceability.IsServiceable)
            throw new InvalidOperationException(
                serviceability.UnavailableReason ?? "Delivery is not available to the selected address.");

        // ── 5. Revalidate products + stock, compute server-side totals ─────────
        var productIds   = cartItems.Select(i => i.ProductId).Distinct().ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(productIds, ct);

        var orderItems   = new List<OrderItem>();
        decimal subTotal = 0m;

        foreach (var ci in cartItems)
        {
            var product = await _productRepo.GetByIdAsync(ci.ProductId, ct);

            if (product is null || !product.IsActive)
                throw new InvalidOperationException(
                    $"Product (ID {ci.ProductId}) is no longer available. Please update your cart.");

            inventoryMap.TryGetValue(ci.ProductId, out var inv);

            if (inv is null || !inv.IsInStock)
                throw new InvalidOperationException(
                    $"'{product.Name}' is currently out of stock.");

            if (ci.Quantity <= 0 || ci.Quantity > 20)
                throw new InvalidOperationException(
                    $"Invalid quantity for '{product.Name}'.");

            if (inv.AvailableQuantity < ci.Quantity)
                throw new InvalidOperationException(
                    $"Only {inv.AvailableQuantity} unit(s) of '{product.Name}' are available.");

            // Always use server-side price — never trust the client
            var unitPrice      = product.Price;
            var offerPrice     = product.OfferPrice;
            var effectivePrice = offerPrice ?? unitPrice;
            var lineTotal      = Math.Round(effectivePrice * ci.Quantity, 2);

            subTotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId      = product.Id,
                ProductName    = product.Name,
                ProductSKU     = product.SKU,
                UnitLabel      = product.UnitLabel,
                Quantity       = ci.Quantity,
                UnitPrice      = unitPrice,
                OfferPrice     = offerPrice,
                EffectivePrice = effectivePrice,
                LineTotal      = lineTotal
            });
        }

        // ── 6. Compute delivery fee ────────────────────────────────────────────
        var rawDeliveryFee = serviceability.DeliveryFee ?? DefaultDeliveryFee;
        var freeAbove      = serviceability.FreeDeliveryAbove ?? DefaultFreeDeliveryAbove;
        var deliveryFee    = subTotal >= freeAbove ? 0m : rawDeliveryFee;
        var totalAmount    = subTotal + deliveryFee;

        // Minimum order check
        var minOrder = serviceability.MinOrderValue;
        if (minOrder.HasValue && subTotal < minOrder.Value)
            throw new InvalidOperationException(
                $"A minimum order of ₹{minOrder:N2} is required for delivery to this area. " +
                $"Your subtotal is ₹{subTotal:N2}.");

        // ── 7. Build order model ──────────────────────────────────────────────
        var orderNumber = GenerateOrderNumber();

        var order = new Order
        {
            OrderNumber         = orderNumber,
            UserId              = userId,
            ShipToAddressLine1  = $"{address.HouseNumber}, {address.BuildingStreet}",
            ShipToAddressLine2  = string.IsNullOrWhiteSpace(address.Locality)
                                      ? address.Landmark
                                      : address.Locality + (address.Landmark != null ? $", near {address.Landmark}" : ""),
            ShipToCity          = address.City,
            ShipToState         = address.State,
            ShipToPincode       = address.Pincode,
            SubTotal            = subTotal,
            DiscountAmount      = 0m,
            DeliveryFee         = deliveryFee,
            TotalAmount         = totalAmount,
            CouponCode          = null,
            CouponDiscount      = 0m,
            PaymentMethod       = paymentMethod,
            PaymentStatus       = "Pending",
            Status              = "Pending",
            Notes               = request.Notes?.Trim(),
            IdempotencyKey      = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                                      ? null
                                      : request.IdempotencyKey.Trim()
        };

        // ── 8. Persist (transaction inside the repository) ────────────────────
        var createdOrder = await _orderRepo.CreateOrderAsync(order, orderItems, ct);

        // ── 9. Clear cart only after successful order creation ─────────────────
        await _cartRepo.ClearCartAsync(cart.Id, ct);

        _logger.LogInformation(
            "Order {OrderNumber} placed successfully for user {UserId}. " +
            "Total: ₹{Total:N2}, Items: {ItemCount}",
            createdOrder.OrderNumber, userId, totalAmount, orderItems.Count);

        return new PlaceOrderResponse(
            OrderId:                 createdOrder.Id,
            OrderNumber:             createdOrder.OrderNumber,
            OrderDate:               createdOrder.CreatedAt,
            Status:                  createdOrder.Status,
            PaymentMethod:           createdOrder.PaymentMethod,
            PaymentStatus:           createdOrder.PaymentStatus,
            DeliveryAddress:         new OrderAddressSnapshot(
                                         createdOrder.ShipToAddressLine1,
                                         createdOrder.ShipToAddressLine2,
                                         createdOrder.ShipToCity,
                                         createdOrder.ShipToState,
                                         createdOrder.ShipToPincode),
            Items:                   orderItems.Select(ToSnapshot).ToList(),
            SubTotal:                createdOrder.SubTotal,
            DeliveryFee:             createdOrder.DeliveryFee,
            DiscountAmount:          createdOrder.DiscountAmount,
            TotalAmount:             createdOrder.TotalAmount,
            EstimatedDeliveryWindow: serviceability.EstimatedDeliveryWindow
        );
    }

    // ── GetMyOrdersAsync ───────────────────────────────────────────────────────

    public async Task<OrdersPagedResponse> GetMyOrdersAsync(
        string userId,
        int page,
        int pageSize,
        string? statusFilter,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default)
    {
        var (orders, totalCount) = await _orderRepo.GetByUserIdAsync(userId, page, pageSize, statusFilter, dateFrom, dateTo, ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var dtos = orders.Select(o => new OrderSummaryDto(
            OrderId:       o.Id,
            OrderNumber:   o.OrderNumber,
            OrderDate:     o.CreatedAt,
            ItemCount:     o.ItemCount,
            TotalAmount:   o.TotalAmount,
            Status:        o.Status,
            PaymentMethod: o.PaymentMethod,
            PaymentStatus: o.PaymentStatus)).ToList();

        return new OrdersPagedResponse(dtos, page, pageSize, totalCount, totalPages);
    }

    // ── GetOrderDetailAsync ────────────────────────────────────────────────────

    public async Task<OrderDetailDto?> GetOrderDetailAsync(
        int orderId, string userId, CancellationToken ct = default)
    {
        var (order, items) = await _orderRepo.GetDetailByIdAndUserIdAsync(orderId, userId, ct);
        if (order is null) return null;

        var history = await _orderRepo.GetStatusHistoryAsync(orderId, ct);

        var canCancel = order.Status is "Pending" or "Confirmed";

        return new OrderDetailDto(
            OrderId:         order.Id,
            OrderNumber:     order.OrderNumber,
            OrderDate:       order.CreatedAt,
            Status:          order.Status,
            PaymentMethod:   order.PaymentMethod,
            PaymentStatus:   order.PaymentStatus,
            DeliveryAddress: new OrderAddressSnapshot(
                                 order.ShipToAddressLine1,
                                 order.ShipToAddressLine2,
                                 order.ShipToCity,
                                 order.ShipToState,
                                 order.ShipToPincode),
            Items:           items.Select(ToSnapshot).ToList(),
            SubTotal:        order.SubTotal,
            DeliveryFee:     order.DeliveryFee,
            DiscountAmount:  order.DiscountAmount,
            TotalAmount:     order.TotalAmount,
            Notes:           order.Notes,
            StatusHistory:   history.Select(h => new OrderStatusHistoryDto(
                                 h.Status, h.Notes, h.ChangedAt)).ToList(),
            CanCancel:       canCancel
        );
    }

    // ── CancelOrderAsync ───────────────────────────────────────────────────────

    public async Task<CancelOrderResponse> CancelOrderAsync(
        int orderId, string userId, CancellationToken ct = default)
    {
        var cancelled = await _orderRepo.CancelOrderAsync(orderId, userId, userId, ct);

        if (!cancelled)
        {
            // Check if the order even exists (ownership vs status reason)
            var order = await _orderRepo.GetByIdAndUserIdAsync(orderId, userId, ct);

            if (order is null)
                return new CancelOrderResponse(false, "Order not found.", null);

            return new CancelOrderResponse(
                false,
                $"Order cannot be cancelled — it is currently {order.Status}.",
                order.Status);
        }

        return new CancelOrderResponse(true, "Order cancelled successfully.", "Cancelled");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string GenerateOrderNumber()
    {
        // Format: GM-YYYYMMDD-XXXXXXXX  (8-char hex from Guid)
        var datePart   = DateTime.UtcNow.ToString("yyyyMMdd");
        var uniquePart = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"GM-{datePart}-{uniquePart}";
    }

    private static OrderItemSnapshot ToSnapshot(OrderItem item) =>
        new(item.ProductId, item.ProductName, item.ProductSKU, item.UnitLabel,
            item.Quantity, item.UnitPrice, item.OfferPrice, item.EffectivePrice, item.LineTotal);

    private async Task<PlaceOrderResponse> BuildConfirmationFromOrder(Order order, CancellationToken ct)
    {
        var (_, items) = await _orderRepo.GetDetailByIdAndUserIdAsync(order.Id, order.UserId, ct);
        return new PlaceOrderResponse(
            OrderId:                 order.Id,
            OrderNumber:             order.OrderNumber,
            OrderDate:               order.CreatedAt,
            Status:                  order.Status,
            PaymentMethod:           order.PaymentMethod,
            PaymentStatus:           order.PaymentStatus,
            DeliveryAddress:         new OrderAddressSnapshot(
                                         order.ShipToAddressLine1,
                                         order.ShipToAddressLine2,
                                         order.ShipToCity,
                                         order.ShipToState,
                                         order.ShipToPincode),
            Items:                   items.Select(ToSnapshot).ToList(),
            SubTotal:                order.SubTotal,
            DeliveryFee:             order.DeliveryFee,
            DiscountAmount:          order.DiscountAmount,
            TotalAmount:             order.TotalAmount,
            EstimatedDeliveryWindow: null);
    }
}
