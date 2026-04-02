using ApiThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm dịch vụ cần thiết
builder.Services.AddControllersWithViews();

// 2. Kết nối Database SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WebContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// 2. Cấu hình Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapGet("/", context => {
    context.Response.Redirect("/admin/index");
    return System.Threading.Tasks.Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=admin}/{action=index}/{id?}");

app.Run();