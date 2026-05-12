using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace ApiThaoCamVien.Filters;

/// <summary>
/// Filter toàn cục cho mọi MVC action: bắt MỌI loại exception phát sinh do
/// client cancel (Ctrl+C python, timeout, đóng tab browser).
///
/// Bắt 3 loại:
///   1. OperationCanceledException (.NET cancel)
///   2. SqlException khi RequestAborted.IsCancellationRequested
///      (SQL Server kill command vì client disconnect — message
///       'Operation cancelled by user', 'severe error occurred')
///   3. Bất kỳ Exception nào khi RequestAborted đã cancel
///      (DbUpdateException wrap SqlException, IOException network blip...)
///
/// Mọi case → trả 499 Client Closed Request, đánh dấu ExceptionHandled.
/// Visual Studio không hiện popup user-unhandled.
/// </summary>
public sealed class OperationCanceledExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (IsClientCancellation(context))
        {
            context.ExceptionHandled = true;
            if (!context.HttpContext.Response.HasStarted)
            {
                context.Result = new StatusCodeResult(499);
            }
        }
    }

    private static bool IsClientCancellation(ExceptionContext context)
    {
        // Case 1: explicit OperationCanceledException
        if (context.Exception is OperationCanceledException) return true;

        // Case 2 & 3: bất kỳ exception nào (SqlException, DbUpdateException,
        // IOException...) khi client đã đóng connection.
        if (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        // Case 2 đặc biệt: SQL Server đôi khi báo cancel SAU khi connection
        // đã rebind — check message text.
        if (context.Exception is SqlException sqlEx)
        {
            var msg = sqlEx.Message ?? "";
            if (msg.Contains("cancelled by user", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("canceled by user", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Operation cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // InnerException check cho DbUpdateException wrap SqlException
        if (context.Exception.InnerException is SqlException innerSql)
        {
            var msg = innerSql.Message ?? "";
            if (msg.Contains("cancelled", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("canceled", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
