using System.Net;
using Arkive.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Arkive.Functions.Middleware;

/// <summary>
/// Helper methods for creating standardized API responses.
/// </summary>
public static class ResponseEnvelopeHelper
{
    public static IActionResult Ok<T>(T data)
    {
        return new OkObjectResult(new ApiResponse<T> { Data = data });
    }

    public static IActionResult OkPaged<T>(T data, int page, int pageSize, int totalCount)
    {
        return new OkObjectResult(new ApiResponse<T>
        {
            Data = data,
            Meta = new ApiMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            }
        });
    }

    public static IActionResult Error(HttpStatusCode statusCode, string code, string message, string traceId, object? details = null)
    {
        return new ObjectResult(new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = details,
                TraceId = traceId
            }
        })
        {
            StatusCode = (int)statusCode
        };
    }

    public static IActionResult NotFound(string message, string traceId)
    {
        return Error(HttpStatusCode.NotFound, "NOT_FOUND", message, traceId);
    }

    public static IActionResult BadRequest(string message, string traceId, object? details = null)
    {
        return Error(HttpStatusCode.BadRequest, "BAD_REQUEST", message, traceId, details);
    }

    public static IActionResult Unauthorized(string traceId)
    {
        return Error(HttpStatusCode.Unauthorized, "UNAUTHORIZED", "Authentication required.", traceId);
    }

    public static IActionResult Forbidden(string traceId)
    {
        return Error(HttpStatusCode.Forbidden, "FORBIDDEN", "Insufficient permissions.", traceId);
    }

    public static IActionResult Created<T>(string location, T data)
    {
        return new CreatedResult(location, new ApiResponse<T> { Data = data });
    }

    public static IActionResult Conflict(string message, string traceId)
    {
        return Error(HttpStatusCode.Conflict, "CONFLICT", message, traceId);
    }

    public static IActionResult NoContent()
    {
        return new NoContentResult();
    }
}
