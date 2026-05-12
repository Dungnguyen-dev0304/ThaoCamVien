using ApiThaoCamVien.Filters;
using ApiThaoCamVien.Models;
using ApiThaoCamVien.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. Database — pool đủ lớn để chịu 50+ device đồng thời ────────────
// Mỗi HTTP request mượn 1 DbContext từ pool, trả lại khi xong. PoolSize=256
// cho phép ~256 request có DB query song song; số request không cần DB
// (presence ping nhẹ) không bị giới hạn bởi đây.
// Max Pool Size=400 trong appsettings.json (raw SQL connection pool).
builder.Services.AddDbContextPool<WebContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(30)
                  .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null)),
    poolSize: 256);

// ─── 2. Controllers + JSON + Global Filters ─────────────────────────────────
builder.Services.AddControllers(options =>
{
    // Layer 2: bắt OperationCanceledException toàn cục cho mọi MVC action
    // → Visual Studio không popup user-unhandled khi client cancel.
    options.Filters.Add<OperationCanceledExceptionFilter>();
}).AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Không encode "&" thành "&" — URL VNPay phải giữ nguyên
    x.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// Layer 3: IExceptionHandler — backup cuối cùng ở middleware level
builder.Services.AddExceptionHandler<CancellationExceptionHandler>();
builder.Services.AddProblemDetails();

// ─── 3. CORS ─────────────────────────────────────────────────────────────────
// QUAN TRỌNG: AllowAnyOrigin phải được set để mobile app gọi được API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()   // Cho phép từ emulator (10.0.2.2), máy thật, web
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ─── 4. Swagger ──────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ─── 5. Logging ──────────────────────────────────────────────────────────────
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ─── 6. POI đa ngôn ngữ (Scoped: dùng chung vòng đời DbContext, an toàn khi nhiều request song song)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PoiLocalizationService>();

// ─── 6b. Hàng đợi FIFO khi nhiều người cùng quét 1 QR (Scoped: dùng chung DbContext của request)
builder.Services.AddScoped<QrQueueService>();

// ─── 7. UDP server discovery (cho app mobile auto-detect IP LAN) ──────────────
builder.Services.AddHostedService<UdpDiscoveryService>();

// ─── 8. MoMo Payment Service (Singleton: read-only config) ──────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MoMoService>();

var app = builder.Build();

// ─── Middleware Pipeline ──────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ThaoCamVien API v1");
    });
}

// ─── Exception handler pipeline ─────────────────────────────────────────
// Bắt OperationCanceledException ở 3 lớp:
//   1. Service: try/catch trong QrQueueService → trả sentinel value
//   2. ExceptionFilter: OperationCanceledExceptionFilter cho mọi MVC action
//   3. IExceptionHandler: CancellationExceptionHandler — backup ở middleware
// Kết quả: VS không bao giờ popup "user-unhandled" khi client Ctrl+C.
app.UseExceptionHandler();

// QUAN TRỌNG: UseCors() phải TRƯỚC UseAuthorization() và MapControllers()
app.UseCors();

// Phục vụ ảnh POI từ cùng thư mục mà Web admin upload (WebThaoCamVien/wwwroot).
// Khi deploy tách server, hãy copy wwwroot hoặc cấu hình reverse proxy tới static.
var webWwwRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "WebThaoCamVien", "wwwroot"));
if (Directory.Exists(webWwwRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(webWwwRoot),
        RequestPath = ""
    });
    app.Logger.LogInformation("Static files: serving wwwroot from {Path}", webWwwRoot);
}
else
{
    app.Logger.LogWarning("Static files: Web wwwroot not found at {Path}. /images/pois/* may 404.", webWwwRoot);
}

// KHÔNG dùng UseHttpsRedirection() khi test với emulator Android
// vì emulator không trust self-signed cert
// app.UseHttpsRedirection();  // ← BỎ COMMENT NÀY NẾU DEPLOY PRODUCTION

app.UseAuthorization();
app.MapControllers();

// ─── Startup log ─────────────────────────────────────────────────────────────
app.Logger.LogInformation("===========================================");
app.Logger.LogInformation("ThaoCamVien API started");
app.Logger.LogInformation("Test URL: http://localhost:5281/api/Pois");
app.Logger.LogInformation("Health:   http://localhost:5281/api/Pois/health");
app.Logger.LogInformation("Swagger:  http://localhost:5281/swagger");
app.Logger.LogInformation("===========================================");

app.Run();
