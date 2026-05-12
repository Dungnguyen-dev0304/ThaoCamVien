using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApiThaoCamVien.Filters;

/// <summary>
/// Filter toàn cục cho mọi MVC action: bắt OperationCanceledException khi
/// client cancel (Ctrl+C python, đóng tab browser, mạng rớt) → trả 499.
///
/// Đây là layer 2 trong chiến lược 3 lớp suppress cancellation popup:
///   1. Service: try/catch + sentinel value (sâu nhất)
///   2. ExceptionFilter (lớp này) — bắt nếu service nào quên catch
///   3. IExceptionHandler (Program.cs) — backup cuối cùng
///
/// Tác dụng: Visual Studio không hiện popup "user-unhandled" nữa vì
/// exception được CATCH trong user code (filter này) trước khi bubble ra
/// non-user code (ASP.NET runtime).
/// </summary>
public sealed class OperationCanceledExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is OperationCanceledException)
        {
            // Đánh dấu đã handle để middleware sau không xử lý lại
            context.ExceptionHandled = true;
            context.Result = new StatusCodeResult(499);   // Nginx-style Client Closed Request
        }
    }
}
