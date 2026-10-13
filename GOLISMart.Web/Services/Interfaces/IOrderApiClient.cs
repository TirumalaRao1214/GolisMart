namespace GOLISMart.Web.Services.Interfaces;

public interface IOrderApiClient
{
    Task<PlaceOrderResponse?> PlaceOrderAsync(
        string token,
        PlaceOrderRequest request,
        CancellationToken ct = default);

    Task<OrdersPagedResponse?> GetMyOrdersAsync(
        string token,
        int page = 1,
        int pageSize = 10,
        string? status = null,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default);

    Task<OrderDetailDto?> GetOrderDetailAsync(
        string token,
        int orderId,
        CancellationToken ct = default);

    Task<CancelOrderResponse?> CancelOrderAsync(
        string token,
        int orderId,
        CancellationToken ct = default);
}
