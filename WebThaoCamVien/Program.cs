var builder = WebApplication.CreateBuilder(args);

// 1. Thêm dịch vụ cần thiết
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 2. Cấu hình Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// 3. Thiết lập trang mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();