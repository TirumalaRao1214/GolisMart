namespace GOLISMart.API.DTOs;

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? ImagePath,
    int SortOrder,
    int ProductCount
);
