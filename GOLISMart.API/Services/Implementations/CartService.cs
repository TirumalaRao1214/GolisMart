using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

public class CartService : ICartService
{
    private const decimal MaxQuantityPerLine = 20m;

    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ILogger<CartService> _logger;

    public CartService(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        ILogger<CartService> logger)
    {
        _cartRepo      = cartRepo;
        _productRepo   = productRepo;
        _inventoryRepo = inventoryRepo;
        _logger        = logger;
    }

    public async Task<CartDto> GetCartAsync(string sessionKey, CancellationToken ct = default)
    {
        var cart = await _cartRepo.GetBySessionKeyAsync(sessionKey, ct);
        if (cart is null)
            return EmptyCart(sessionKey);

        var items = await _cartRepo.GetItemsAsync(cart.Id, ct);
        return await BuildCartDtoAsync(cart.Id, sessionKey, items, ct);
    }

    public async Task<CartDto> AddItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default)
    {
        if (quantity <= 0 || quantity > MaxQuantityPerLine)
            throw new ArgumentException($"Quantity must be between 1 and {MaxQuantityPerLine}.");

        var product = await _productRepo.GetByIdAsync(productId, ct)
            ?? throw new KeyNotFoundException($"Product {productId} not found.");

        if (!product.IsActive)
            throw new ArgumentException("Product is not available.");

        var inventory = await _inventoryRepo.GetByProductIdAsync(productId, ct);
        if (inventory is null || !inventory.IsInStock)
            throw new ArgumentException("Product is out of stock.");

        // Check requested quantity does not exceed available stock
        if (quantity > inventory.AvailableQuantity)
            throw new ArgumentException($"Only {inventory.AvailableQuantity} units available.");

        var unitPrice = product.OfferPrice ?? product.Price;

        var cart = await _cartRepo.GetOrCreateBySessionKeyAsync(sessionKey, ct);

        // Check if already in cart — if sum would exceed max, cap at max
        var existingItems = await _cartRepo.GetItemsAsync(cart.Id, ct);
        var existingItem  = existingItems.FirstOrDefault(i => i.ProductId == productId);
        var newTotal = (existingItem?.Quantity ?? 0m) + quantity;
        if (newTotal > MaxQuantityPerLine)
            throw new ArgumentException($"Cannot add more than {MaxQuantityPerLine} of the same product.");

        await _cartRepo.UpsertItemAsync(cart.Id, productId, quantity, unitPrice, ct);
        await _cartRepo.TouchCartAsync(cart.Id, ct);

        var items = await _cartRepo.GetItemsAsync(cart.Id, ct);
        return await BuildCartDtoAsync(cart.Id, sessionKey, items, ct);
    }

    public async Task<CartDto> UpdateItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0. Use remove to delete an item.");
        if (quantity > MaxQuantityPerLine)
            throw new ArgumentException($"Maximum quantity per item is {MaxQuantityPerLine}.");

        var cart = await _cartRepo.GetBySessionKeyAsync(sessionKey, ct)
            ?? throw new KeyNotFoundException("Cart not found.");

        var inventory = await _inventoryRepo.GetByProductIdAsync(productId, ct);
        if (inventory is null || !inventory.IsInStock)
            throw new ArgumentException("Product is out of stock.");
        if (quantity > inventory.AvailableQuantity)
            throw new ArgumentException($"Only {inventory.AvailableQuantity} units available.");

        await _cartRepo.UpdateItemQuantityAsync(cart.Id, productId, quantity, ct);
        await _cartRepo.TouchCartAsync(cart.Id, ct);

        var items = await _cartRepo.GetItemsAsync(cart.Id, ct);
        return await BuildCartDtoAsync(cart.Id, sessionKey, items, ct);
    }

    public async Task<CartDto> RemoveItemAsync(string sessionKey, int productId, CancellationToken ct = default)
    {
        var cart = await _cartRepo.GetBySessionKeyAsync(sessionKey, ct)
            ?? throw new KeyNotFoundException("Cart not found.");

        await _cartRepo.RemoveItemAsync(cart.Id, productId, ct);
        await _cartRepo.TouchCartAsync(cart.Id, ct);

        var items = await _cartRepo.GetItemsAsync(cart.Id, ct);
        return await BuildCartDtoAsync(cart.Id, sessionKey, items, ct);
    }

    public async Task<CartDto> ClearCartAsync(string sessionKey, CancellationToken ct = default)
    {
        var cart = await _cartRepo.GetBySessionKeyAsync(sessionKey, ct);
        if (cart is not null)
        {
            await _cartRepo.ClearCartAsync(cart.Id, ct);
            await _cartRepo.TouchCartAsync(cart.Id, ct);
        }
        return EmptyCart(sessionKey);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<CartDto> BuildCartDtoAsync(int cartId, string sessionKey,
        IReadOnlyList<GOLISMart.API.Models.CartItem> items, CancellationToken ct)
    {
        if (items.Count == 0)
            return EmptyCart(sessionKey, cartId);

        var productIds   = items.Select(i => i.ProductId).Distinct().ToList();
        var products     = new Dictionary<int, GOLISMart.API.Models.Product>();
        var inventoryMap = new Dictionary<int, GOLISMart.API.Models.InventoryStatus>();

        foreach (var id in productIds)
        {
            var p = await _productRepo.GetByIdAsync(id, ct);
            if (p is not null) products[id] = p;
        }

        var invMap = await _inventoryRepo.GetByProductIdsAsync(productIds, ct);
        foreach (var kv in invMap) inventoryMap[kv.Key] = kv.Value;

        var itemDtos = items.Select(ci =>
        {
            products.TryGetValue(ci.ProductId, out var p);
            inventoryMap.TryGetValue(ci.ProductId, out var inv);

            var currentPrice  = p is not null ? (p.OfferPrice ?? p.Price) : ci.UnitPrice;
            var originalPrice = p?.Price ?? ci.UnitPrice;
            var inStock       = inv?.IsInStock ?? false;
            var isAvailable   = p?.IsActive == true && inStock;

            return new CartItemDto(
                ProductId:     ci.ProductId,
                Name:          p?.Name ?? "Unknown Product",
                Slug:          p?.Slug ?? string.Empty,
                UnitLabel:     p?.UnitLabel ?? string.Empty,
                ImagePath:     p?.ImagePath,
                Quantity:      ci.Quantity,
                UnitPrice:     currentPrice,
                OriginalPrice: originalPrice,
                LineTotal:     Math.Round(currentPrice * ci.Quantity, 2),
                IsInStock:     inStock,
                IsAvailable:   isAvailable
            );
        }).ToList();

        var subTotal   = itemDtos.Where(i => i.IsAvailable).Sum(i => i.LineTotal);
        var totalItems = (int)Math.Ceiling(itemDtos.Sum(i => (double)i.Quantity));

        return new CartDto(cartId, sessionKey, itemDtos, subTotal, totalItems);
    }

    private static CartDto EmptyCart(string sessionKey, int cartId = 0) =>
        new(cartId, sessionKey, [], 0m, 0);
}
