using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace ApiThaoCamVien.Filters;

/// <summary>
/// Layer 3 backup: bắt mọi exception ở middleware level nếu lỡ filter MVC
/// không bắt được. Cùng logic detect cancellation như filter.
/// </summary>
public sealed class CancellationExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (IsClientCancellation(exception, httpContext))
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = 499;
            }
            return ValueTask.FromResult(true);
        }
        return ValueTask.FromResult(false);
    }

    private static bool IsClientCancellation(Exception ex, HttpContext ctx)
    {
        if (ex is OperationCanceledException) return true;
        if (ctx.RequestAborted.IsCancellationRequested) return true;

        if (ex is SqlException sqlEx && ContainsCancelText(sqlEx.Message)) return true;
        if (ex.InnerException is SqlException innerSql && ContainsCancelText(innerSql.Message))
            return true;

        return false;
    }

    private static bool ContainsCancelText(string? msg) =>
        !string.IsNullOrEmpty(msg) &&
        (msg.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("canceled", StringComparison.OrdinalIgnoreCase));
}
