using ApiThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm dịch vụ cần thiết
builder.Services.AddControllersWithViews();

// 2. Kết nối Database SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WebContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Cấu hình xác thực Cookie (MỚI THÊM)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Đường dẫn trang đăng nhập
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

var app = builder.Build();

// Pipeline cấu hình
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 4. Kích hoạt Authentication (PHẢI nằm trước Authorization)
app.UseAuthentication();
app.UseAuthorization();

// Điều hướng mặc định
app.MapGet("/", context => {
    context.Response.Redirect("/Account/Login");
    return System.Threading.Tasks.Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();