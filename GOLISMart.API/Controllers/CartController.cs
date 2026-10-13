using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/cart")]
[Produces("application/json")]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    private readonly ILogger<CartController> _logger;
    private const string SessionKeyHeader = "X-Session-Key";

    public CartController(ICartService cart, ILogger<CartController> logger)
    {
        _cart   = cart;
        _logger = logger;
    }

    private IActionResult SessionKeyMissing() =>
        BadRequest(new ErrorResponse("X-Session-Key header is required."));

    [HttpGet]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCart(CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue(SessionKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return SessionKeyMissing();

        var cart = await _cart.GetCartAsync(key!, ct);
        return Ok(cart);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request, CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue(SessionKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return SessionKeyMissing();

        try
        {
            var cart = await _cart.AddItemAsync(key!, request.ProductId, request.Quantity, ct);
            return Ok(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPut("items/{productId:int}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(int productId, [FromBody] UpdateCartItemRequest request, CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue(SessionKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return SessionKeyMissing();

        try
        {
            var cart = await _cart.UpdateItemAsync(key!, productId, request.Quantity, ct);
            return Ok(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete("items/{productId:int}")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(int productId, CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue(SessionKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return SessionKeyMissing();

        try
        {
            var cart = await _cart.RemoveItemAsync(key!, productId, ct);
            return Ok(cart);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpDelete]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearCart(CancellationToken ct = default)
    {
        if (!Request.Headers.TryGetValue(SessionKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return SessionKeyMissing();

        var cart = await _cart.ClearCartAsync(key!, ct);
        return Ok(cart);
    }
}
