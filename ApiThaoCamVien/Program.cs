using ApiThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. Database ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<WebContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(30)));

// ─── 2. Controllers + JSON ───────────────────────────────────────────────────
builder.Services.AddControllers().AddJsonOptions(x =>
{
    // Chống vòng lặp vô tận (navigation properties)
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    // Trả về null thay vì bỏ qua field
    x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

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
