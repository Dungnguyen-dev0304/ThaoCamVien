using SharedThaoCamVien.Models;
using System.Collections.ObjectModel;

namespace AppThaoCamVien.Pages;

public partial class AnimalListPage : ContentPage
{
    // Danh sách để bind ra giao diện
    public ObservableCollection<Poi> AnimalsList { get; set; } = new ObservableCollection<Poi>();

    public AnimalListPage()
    {
        InitializeComponent();

        // Tạo dữ liệu mô phỏng giống trong ảnh mẫu
        LoadMockData();

        // Gán nguồn dữ liệu cho CollectionView
        AnimalsCollectionView.ItemsSource = AnimalsList;
    }

    private void LoadMockData()
    {
        // Bạn có thể lấy danh sách này từ DatabaseService trong thực tế
        AnimalsList.Add(new Poi { PoiId = 1, Name = "Hổ Đông Dương", Description = "Chúa tể sơn lâm rừng nhiệt đới, biểu tượng sức mạnh.", ImageThumbnail = "tiger.jpg" });
        AnimalsList.Add(new Poi { PoiId = 2, Name = "Voi Châu Á", Description = "Loài động vật trên cạn lớn nhất Châu Á, rất thông minh.", ImageThumbnail = "elephant.jpg" });
        AnimalsList.Add(new Poi { PoiId = 3, Name = "Cá sấu nước ngọt", Description = "Sát thủ đầm lầy với bộ hàm cực khỏe.", ImageThumbnail = "crocodile.png" }); // Đổi đuôi ảnh tương ứng
        AnimalsList.Add(new Poi { PoiId = 4, Name = "Lạc đà hai bướu", Description = "Loài vật chịu hạn siêu hạng của sa mạc Gobi.", ImageThumbnail = "camel.png" });
        AnimalsList.Add(new Poi { PoiId = 5, Name = "Công xanh", Description = "Sở hữu bộ lông đuôi rực rỡ tuyệt đẹp dùng để múa gọi bạn tình.", ImageThumbnail = "peacock.png" });
        AnimalsList.Add(new Poi { PoiId = 6, Name = "Hà mã", Description = "Dù béo tròn nhưng chúng bơi lội cực giỏi và chạy rất nhanh.", ImageThumbnail = "hippo.png" });
        AnimalsList.Add(new Poi { PoiId = 7, Name = "Hồng hạc", Description = "Loài chim có đôi chân dài và bộ lông màu hồng đặc trưng.", ImageThumbnail = "flamingo.png" });
        AnimalsList.Add(new Poi { PoiId = 8, Name = "Trăn đất", Description = "Loài trăn khổng lồ có khả năng siết mồi mạnh mẽ.", ImageThumbnail = "snake.png" });
    }

    // Sự kiện khi nhấn vào MỘT THẺ CON VẬT
    private async void OnAnimalTapped(object sender, TappedEventArgs e)
    {
        // Lấy dữ liệu của con vật được nhấn từ CommandParameter
        if (e.Parameter is Poi selectedAnimal)
        {
            // Hiệu ứng nhấp nháy mờ thẻ khi bấm cho đẹp (UX)
            if (sender is View view)
            {
                await view.FadeTo(0.5, 100);
                await view.FadeTo(1, 100);
            }

            // Gọi StoryAudioPage thông qua Service Provider (để giữ nguyên các Service như AudioService bên trong nó)
            var storyPage = Handler?.MauiContext?.Services.GetService<StoryAudioPage>();

            if (storyPage != null)
            {
                // Truyền dữ liệu con vật vừa chọn sang trang StoryAudioPage
                storyPage.LoadPoi(selectedAnimal);

                // Chuyển trang!
                await Navigation.PushAsync(storyPage);
            }
            else
            {
                await DisplayAlert("Lỗi", "Không thể tải trang Audio. Hãy kiểm tra lại cấu hình Dependency Injection.", "OK");
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}