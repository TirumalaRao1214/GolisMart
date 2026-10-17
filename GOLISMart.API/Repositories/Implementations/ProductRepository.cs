using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ProductRepository> _logger;
    private const int QueryTimeout = 30;

    // Allowlist of sortable columns mapped to their SQL expressions
    private static readonly IReadOnlyDictionary<string, string> SortableColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"]       = "p.Name",
            ["price"]      = "p.Price",
            ["offerprice"] = "ISNULL(p.OfferPrice, p.Price)",
            ["createdat"]  = "p.CreatedAt",
        };

    public ProductRepository(IDbConnectionFactory connectionFactory, ILogger<ProductRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<PagedResult<Product>> GetActiveProductsAsync(ProductQueryParams query, CancellationToken ct = default)
    {
        // Clamp pagination
        var page     = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Resolve sort column safely (allowlist)
        var sortCol = SortableColumns.TryGetValue(query.SortBy ?? "name", out var col) ? col : "p.Name";
        var sortDir = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        var conditions = new List<string> { "p.IsActive = 1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Partial, case-insensitive (column collation is *_CI_AS) match on name, description, SKU and category.
            // LIKE wildcards typed by the user are escaped so they are matched literally.
            conditions.Add("(p.Name LIKE @Search ESCAPE '\\' OR p.Description LIKE @Search ESCAPE '\\' " +
                           "OR p.SKU LIKE @Search ESCAPE '\\' OR c.Name LIKE @Search ESCAPE '\\')");
            parameters.Add("Search", $"%{EscapeLike(query.Search.Trim())}%", System.Data.DbType.String);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            conditions.Add("c.Slug = @CategorySlug");
            parameters.Add("CategorySlug", query.Category.Trim());
        }

        if (query.InStock == true)
        {
            conditions.Add("(i.QuantityOnHand - i.ReservedQuantity) > 0");
        }

        var where = string.Join(" AND ", conditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            LEFT   JOIN Inventory i ON i.ProductId = p.Id
            WHERE  {where};
            """;

        // Only the columns needed to render product cards (no Description / timestamps)
        var dataSql = $"""
            SELECT p.Id, p.CategoryId, p.SKU, p.Name, p.Slug,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath,
                   c.Name  AS CategoryName,
                   c.Slug  AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            LEFT   JOIN Inventory i ON i.ProductId = p.Id
            WHERE  {where}
            ORDER  BY {sortCol} {sortDir}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        _logger.LogDebug("Fetching products page {Page} pageSize {PageSize} search={Search} category={Category}",
            page, pageSize, query.Search, query.Category);

        using var conn = _connectionFactory.CreateConnection();
        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, commandTimeout: QueryTimeout, cancellationToken: ct));

        var items = await conn.QueryAsync<Product>(
            new CommandDefinition(dataSql, parameters, commandTimeout: QueryTimeout, cancellationToken: ct));

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResult<Product>(items.AsList(), page, pageSize, totalCount, totalPages);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.CategoryId, p.SKU, p.Name, p.Slug, p.Description,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath, p.CreatedAt, p.UpdatedAt,
                   c.Name AS CategoryName, c.Slug AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            WHERE  p.Id = @Id;
            """;

        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Product>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: QueryTimeout, cancellationToken: ct));
    }

    public async Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id, p.CategoryId, p.SKU, p.Name, p.Slug, p.Description,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath, p.CreatedAt, p.UpdatedAt,
                   c.Name AS CategoryName, c.Slug AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            WHERE  p.Slug = @Slug;
            """;

        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Product>(
            new CommandDefinition(sql, new { Slug = slug }, commandTimeout: QueryTimeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesByProductIdAsync(int productId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, ProductId, ImagePath, AltText, SortOrder, IsPrimary
            FROM   ProductImages
            WHERE  ProductId = @ProductId
            ORDER  BY SortOrder, Id;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<ProductImage>(
            new CommandDefinition(sql, new { ProductId = productId }, commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetFeaturedProductsAsync(int maxCount = 8, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@MaxCount)
                   p.Id, p.CategoryId, p.SKU, p.Name, p.Slug, p.Description,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath, p.CreatedAt, p.UpdatedAt,
                   c.Name AS CategoryName, c.Slug AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            WHERE  p.IsActive = 1 AND p.IsFeatured = 1
            ORDER  BY p.UpdatedAt DESC;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<Product>(
            new CommandDefinition(sql, new { MaxCount = maxCount }, commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetRelatedProductsAsync(int productId, int categoryId, int maxCount = 6, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@MaxCount)
                   p.Id, p.CategoryId, p.SKU, p.Name, p.Slug, p.Description,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath, p.CreatedAt, p.UpdatedAt,
                   c.Name AS CategoryName, c.Slug AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            WHERE  p.IsActive = 1
              AND  p.CategoryId = @CategoryId
              AND  p.Id <> @ProductId
            ORDER  BY p.IsFeatured DESC, p.UpdatedAt DESC;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<Product>(
            new CommandDefinition(sql, new { MaxCount = maxCount, CategoryId = categoryId, ProductId = productId },
                commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryIdAsync(int categoryId, int maxCount = 50, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@MaxCount)
                   p.Id, p.CategoryId, p.SKU, p.Name, p.Slug, p.Description,
                   p.UnitCode, p.UnitLabel, p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                   p.ImagePath, p.CreatedAt, p.UpdatedAt,
                   c.Name AS CategoryName, c.Slug AS CategorySlug
            FROM   Products p
            JOIN   Categories c ON c.Id = p.CategoryId
            WHERE  p.IsActive = 1 AND p.CategoryId = @CategoryId
            ORDER  BY p.Name;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<Product>(
            new CommandDefinition(sql, new { MaxCount = maxCount, CategoryId = categoryId },
                commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }
}
