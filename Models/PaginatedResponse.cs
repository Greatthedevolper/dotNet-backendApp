namespace DotNetApi.Models;

public class PaginatedResponse
{
    public required string Message { get; set; }
    public int StatusCode { get; set; }
    public required object Data { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
}

