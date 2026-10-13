using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IOrderService
{
    /// <summary>Places an order for the authenticated customer.</summary>
    Task<PlaceOrderResponse> PlaceOrderAsync(
        PlaceOrderRequest request,
        string userId,
        CancellationToken ct = default);

    /// <summary>Returns a paged list of the customer's orders.</summary>
    Task<OrdersPagedResponse> GetMyOrdersAsync(
        string userId,
        int page,
        int pageSize,
        string? statusFilter,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default);

    /// <summary>Returns the full detail of a single order that belongs to the customer.</summary>
    Task<OrderDetailDto?> GetOrderDetailAsync(
        int orderId,
        string userId,
        CancellationToken ct = default);

    /// <summary>Cancels an eligible order and restores inventory.</summary>
    Task<CancelOrderResponse> CancelOrderAsync(
        int orderId,
        string userId,
        CancellationToken ct = default);
}
