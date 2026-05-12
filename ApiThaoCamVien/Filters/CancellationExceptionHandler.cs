using Microsoft.AspNetCore.Diagnostics;

namespace ApiThaoCamVien.Filters;

/// <summary>
/// Backup cuối cùng (Layer 3): bắt OperationCanceledException ở tầng
/// IExceptionHandler — xử lý exception trong user code trước khi bubble lên
/// host. Đảm bảo VS không hiện popup ngay cả khi cancellation xảy ra
/// trong middleware/host code chứ không phải controller.
/// </summary>
public sealed class CancellationExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = 499;
            }
            return ValueTask.FromResult(true);   // đã handle, không bubble
        }
        return ValueTask.FromResult(false);
    }
}
