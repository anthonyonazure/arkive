namespace Arkive.Core.Models;

public class ApiErrorResponse
{
    public ApiError Error { get; set; } = default!;
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
    public string TraceId { get; set; } = string.Empty;
}
