using ApiThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm d?ch v? c?n thi?t
builder.Services.AddControllersWithViews();

// 2. K?t n?i Database SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WebContext>(options =>
    options.UseSqlServer(connectionString));

// 3. C?u hình xác th?c Cookie (M?I THÊM)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // ???ng d?n trang ??ng nh?p
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

var app = builder.Build();

// Pipeline c?u hình
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 4. Kích ho?t Authentication (PH?I n?m tr??c Authorization)
app.UseAuthentication();
app.UseAuthorization();

// ?i?u h??ng m?c ??nh
//app.MapGet("/", context => {
//    context.Response.Redirect("/Account/Login");
//    return System.Threading.Tasks.Task.CompletedTask;
//});

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=admin}/{action=index}/{id?}");

app.Run();