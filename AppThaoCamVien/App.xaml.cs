using AppThaoCamVien.Services;

namespace AppThaoCamVien;

public partial class App : Application
{
    public App(IServiceProvider sp)
    {
        InitializeComponent();

        // Load ngôn ngữ đã lưu từ lần trước
        LanguageManager.Load();

        // Truyền ServiceProvider vào AppShell để nó tạo Pages đúng cách qua DI
        MainPage = new AppShell(sp);
    }
}
