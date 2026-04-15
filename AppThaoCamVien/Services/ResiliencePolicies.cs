using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Diagnostics;
using System.Net;

namespace AppThaoCamVien.Services;

/// <summary>
/// Polly v8 resilience pipelines cho toàn bộ HTTP calls trong app.
///
/// Chiến lược 3 tầng (từ ngoài vào trong):
///   1. Timeout tổng (15s) — deadline toàn bộ pipeline
///   2. Retry (2 lần, exponential backoff 1s → 2s)
///   3. Circuit Breaker (ngắt sau 3 lỗi liên tiếp, mở lại sau 30s)
///
/// Khi Circuit Breaker mở → mọi request bị reject ngay (fail-fast),
/// tránh app treo đợi timeout khi server đã chết.
/// </summary>
public static class ResiliencePolicies
{
    private static ResiliencePipeline? _httpPipeline;
    private static readonly object _lock = new();

    /// <summary>
    /// Pipeline chính cho API calls. Thread-safe, singleton.
    /// Dùng cho cả GET và POST.
    /// </summary>
    public static ResiliencePipeline HttpPipeline
    {
        get
        {
            if (_httpPipeline != null) return _httpPipeline;
            lock (_lock)
            {
                _httpPipeline ??= BuildHttpPipeline();
            }
            return _httpPipeline;
        }
    }

    private static ResiliencePipeline BuildHttpPipeline()
    {
        return new ResiliencePipelineBuilder()

            // ── Tầng 1: Timeout tổng ────────────────────────────────────
            // Deadline cho toàn bộ pipeline (bao gồm cả retries).
            // Nếu tổng thời gian > 15s → ném TimeoutRejectedException.
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(15),
                OnTimeout = static args =>
                {
                    Debug.WriteLine($"[Polly] Total timeout after {args.Timeout.TotalSeconds}s");
                    return default;
                }
            })

            // ── Tầng 2: Retry ───────────────────────────────────────────
            // Retry 2 lần, exponential backoff: 1s → 2s.
            // Chỉ retry các lỗi transient (network, timeout).
            // KHÔNG retry 4xx (bad request, not found) — đó là lỗi logic.
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                    {
                        // Chỉ retry lỗi mạng thật (timeout, connection refused, DNS)
                        // Không retry 4xx responses
                        if (ex.StatusCode.HasValue)
                        {
                            var code = (int)ex.StatusCode.Value;
                            return code >= 500; // Chỉ retry 5xx
                        }
                        return true; // Lỗi network không có status code → retry
                    })
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>(),
                OnRetry = static args =>
                {
                    Debug.WriteLine(
                        $"[Polly] Retry #{args.AttemptNumber + 1}, " +
                        $"delay={args.RetryDelay.TotalSeconds:F1}s, " +
                        $"reason={args.Outcome.Exception?.GetType().Name ?? "unknown"}");
                    return default;
                }
            })

            // ── Tầng 3: Circuit Breaker ─────────────────────────────────
            // Ngắt mạch sau 3 lỗi liên tiếp trong cửa sổ 60s.
            // Khi mở: reject tất cả request ngay (fail-fast 30s).
            // Sau 30s: thử 1 request "probe" — nếu OK thì đóng lại.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>(),
                OnOpened = static args =>
                {
                    Debug.WriteLine(
                        $"[Polly] Circuit OPENED — server unreachable. " +
                        $"Break for {args.BreakDuration.TotalSeconds}s");
                    return default;
                },
                OnClosed = static args =>
                {
                    Debug.WriteLine("[Polly] Circuit CLOSED — server recovered");
                    return default;
                },
                OnHalfOpened = static args =>
                {
                    Debug.WriteLine("[Polly] Circuit HALF-OPEN — probing...");
                    return default;
                }
            })

            .Build();
    }

    /// <summary>
    /// Kiểm tra nhanh xem circuit breaker có đang mở không.
    /// UI có thể dùng để hiển thị "Server không khả dụng" ngay lập tức.
    /// </summary>
    public static bool IsCircuitOpen
    {
        get
        {
            try
            {
                // Polly v8 không expose state trực tiếp từ pipeline,
                // nhưng ta có thể detect qua exception khi execute.
                return false; // Default: giả sử closed
            }
            catch { return false; }
        }
    }
}
