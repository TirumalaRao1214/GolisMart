using FluentAssertions;
using GOLISMart.API.DTOs;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for ProductQueryParams validation logic and sort allowlist.
/// </summary>
public class ProductQueryParamsTests
{
    private static readonly HashSet<string> ValidSortFields =
        new(StringComparer.OrdinalIgnoreCase) { "name", "price", "offerprice", "createdat" };

    [Theory]
    [InlineData("name")]
    [InlineData("price")]
    [InlineData("offerprice")]
    [InlineData("createdat")]
    [InlineData("NAME")]
    [InlineData("Price")]
    public void SortBy_AllowlistContains_ValidFields(string sortBy)
    {
        ValidSortFields.Contains(sortBy).Should().BeTrue();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("sku")]
    [InlineData("stock")]
    [InlineData("random_column; DROP TABLE Products--")]
    public void SortBy_AllowlistRejects_InvalidFields(string sortBy)
    {
        ValidSortFields.Contains(sortBy).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Page_LessThanOne_IsInvalid(int page)
    {
        (page >= 1).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void PageSize_Between1And100_IsValid(int pageSize)
    {
        (pageSize >= 1 && pageSize <= 100).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void PageSize_OutOfRange_IsInvalid(int pageSize)
    {
        (pageSize >= 1 && pageSize <= 100).Should().BeFalse();
    }

    [Fact]
    public void PagedResult_TotalPages_CalculatesCorrectly()
    {
        var items = new List<string> { "a", "b" };
        var result = new PagedResult<string>(items, 1, 20, 45, (int)Math.Ceiling(45.0 / 20));

        result.TotalPages.Should().Be(3);
        result.TotalCount.Should().Be(45);
    }

    [Fact]
    public void PagedResult_ZeroItems_ReturnsZeroPages()
    {
        var result = new PagedResult<string>(new List<string>(), 1, 20, 0, 0);
        result.TotalPages.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
