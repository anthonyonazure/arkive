namespace Arkive.Core.Models;

public class ApiResponse<T>
{
    public T Data { get; set; } = default!;
    public ApiMeta? Meta { get; set; }
}

public class ApiMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
