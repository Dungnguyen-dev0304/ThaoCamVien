using Microsoft.Extensions.DependencyInjection;

namespace AppThaoCamVien
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Load ngôn ngữ từ bộ nhớ ngay khi mở App
            AppThaoCamVien.Services.LanguageManager.LoadCurrentLanguage();

            // Bắt buộc phải có dòng này để gọi Giao diện chính lên
            MainPage = new AppShell();
        }
    }
}