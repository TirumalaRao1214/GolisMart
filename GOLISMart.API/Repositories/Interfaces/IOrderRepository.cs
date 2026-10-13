using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IOrderRepository
{
    /// <summary>Creates order, order items, deducts inventory, and records status history in a single transaction.</summary>
    Task<Order> CreateOrderAsync(
        Order order,
        IReadOnlyList<OrderItem> items,
        CancellationToken ct = default);

    /// <summary>Returns the order only if it belongs to the specified user (ownership check).</summary>
    Task<Order?> GetByIdAndUserIdAsync(int orderId, string userId, CancellationToken ct = default);

    /// <summary>Returns the order and its items for a confirmed owner.</summary>
    Task<(Order? Order, IReadOnlyList<OrderItem> Items)> GetDetailByIdAndUserIdAsync(
        int orderId, string userId, CancellationToken ct = default);

    /// <summary>Returns status history for an order the user owns.</summary>
    Task<IReadOnlyList<OrderStatusHistory>> GetStatusHistoryAsync(int orderId, CancellationToken ct = default);

    /// <summary>Paged list of orders for a customer, newest first.</summary>
    Task<(IReadOnlyList<Order> Orders, int TotalCount)> GetByUserIdAsync(
        string userId,
        int page,
        int pageSize,
        string? statusFilter,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default);

    /// <summary>
    /// Attempts to cancel the order if it is in a cancellable state.
    /// Restores inventory atomically and appends a status history record.
    /// Returns true when cancellation succeeded, false when the current status prevents it.
    /// </summary>
    Task<bool> CancelOrderAsync(int orderId, string userId, string changedBy, CancellationToken ct = default);

    /// <summary>Returns an existing order with the given idempotency key for the user (duplicate detection).</summary>
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, string userId, CancellationToken ct = default);
}
