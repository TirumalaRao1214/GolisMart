using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Web;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class CatalogApiClient : ICatalogApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CatalogApiClient> _logger;

    public CatalogApiClient(HttpClient http, ILogger<CatalogApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<CategoryDto>>("api/v1/categories", ct);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch categories from API");
            return [];
        }
    }

    public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"api/v1/categories/{Uri.EscapeDataString(slug)}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CategoryDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch category '{Slug}' from API", slug);
            return null;
        }
    }

    public async Task<PagedResult<ProductSummaryDto>?> GetProductsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? category = null,
        string? sortBy = "name",
        string? sortOrder = "asc",
        bool? inStock = null,
        CancellationToken ct = default)
    {
        try
        {
            var qs = BuildProductQuery(page, pageSize, search, category, sortBy, sortOrder, inStock);
            var response = await _http.GetAsync($"api/v1/products{qs}", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PagedResult<ProductSummaryDto>>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch products from API");
            return null;
        }
    }

    public async Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"api/v1/products/{Uri.EscapeDataString(slug)}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductDetailDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch product '{Slug}' from API", slug);
            return null;
        }
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedProductsAsync(int max = 8, CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<ProductSummaryDto>>(
                $"api/v1/products/featured?max={max}", ct);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch featured products from API");
            return [];
        }
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(string slug, int max = 6, CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<ProductSummaryDto>>(
                $"api/v1/products/{Uri.EscapeDataString(slug)}/related?max={max}", ct);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch related products for '{Slug}' from API", slug);
            return [];
        }
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetProductsByCategorySlugAsync(string categorySlug, int max = 8, CancellationToken ct = default)
    {
        try
        {
            var result = await GetProductsAsync(page: 1, pageSize: max, category: categorySlug, ct: ct);
            return result?.Items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch products for category '{Slug}' from API", categorySlug);
            return [];
        }
    }

    // -------------------------------------------------------------------------
    private static string BuildProductQuery(int page, int pageSize, string? search,
        string? category, string? sortBy, string? sortOrder, bool? inStock)
    {
        var sb = new StringBuilder("?");
        sb.Append($"page={page}&pageSize={pageSize}");
        if (!string.IsNullOrWhiteSpace(search))
            sb.Append($"&search={HttpUtility.UrlEncode(search)}");
        if (!string.IsNullOrWhiteSpace(category))
            sb.Append($"&category={HttpUtility.UrlEncode(category)}");
        if (!string.IsNullOrWhiteSpace(sortBy))
            sb.Append($"&sortBy={HttpUtility.UrlEncode(sortBy)}");
        if (!string.IsNullOrWhiteSpace(sortOrder))
            sb.Append($"&sortOrder={HttpUtility.UrlEncode(sortOrder)}");
        if (inStock.HasValue)
            sb.Append($"&inStock={inStock.Value.ToString().ToLower()}");
        return sb.ToString();
    }
}
