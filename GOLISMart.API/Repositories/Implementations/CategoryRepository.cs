using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class CategoryRepository : ICategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<CategoryRepository> _logger;
    private const int QueryTimeout = 30;

    public CategoryRepository(IDbConnectionFactory connectionFactory, ILogger<CategoryRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, Slug, Description, ImagePath, SortOrder, IsActive, CreatedAt, UpdatedAt
            FROM   Categories
            WHERE  IsActive = 1
            ORDER  BY SortOrder, Name;
            """;

        using var conn = _connectionFactory.CreateConnection();
        _logger.LogDebug("Fetching active categories");
        var result = await conn.QueryAsync<Category>(
            new CommandDefinition(sql, commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, Slug, Description, ImagePath, SortOrder, IsActive, CreatedAt, UpdatedAt
            FROM   Categories
            WHERE  Slug = @Slug AND IsActive = 1;
            """;

        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            new CommandDefinition(sql, new { Slug = slug }, commandTimeout: QueryTimeout, cancellationToken: ct));
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, Slug, Description, ImagePath, SortOrder, IsActive, CreatedAt, UpdatedAt
            FROM   Categories
            WHERE  Id = @Id;
            """;

        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Category>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: QueryTimeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(Category Category, int ProductCount)>> GetCategoriesWithProductCountAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.Id, c.Name, c.Slug, c.Description, c.ImagePath, c.SortOrder, c.IsActive, c.CreatedAt, c.UpdatedAt,
                   COUNT(p.Id) AS ProductCount
            FROM   Categories c
            LEFT   JOIN Products p ON p.CategoryId = c.Id AND p.IsActive = 1
            WHERE  c.IsActive = 1
            GROUP  BY c.Id, c.Name, c.Slug, c.Description, c.ImagePath, c.SortOrder, c.IsActive, c.CreatedAt, c.UpdatedAt
            ORDER  BY c.SortOrder, c.Name;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var rows = await conn.QueryAsync<Category, int, (Category, int)>(
            new CommandDefinition(sql, commandTimeout: QueryTimeout, cancellationToken: ct),
            (cat, count) => (cat, count),
            splitOn: "ProductCount");
        return rows.ToList();
    }
}
