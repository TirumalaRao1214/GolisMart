namespace GOLISMart.API.DTOs;

public record ErrorResponse(string Error, string? TraceId = null);
