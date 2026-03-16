//namespace AppThaoCamVien.Pages
//{
//    public partial class MapPage : ContentPage
//    {
//        public MapPage()
//        {
//            InitializeComponent();
//        }

//        protected override void OnAppearing()
//        {
//            base.OnAppearing();

//            // Khởi tạo bản đồ miễn phí ngay vị trí bạn muốn
//            // Thêm <!DOCTYPE html> và thẻ <meta name='viewport'...> để ép WebView hiển thị full màn hình
//            string htmlHeader = @"<!DOCTYPE html>
//    <html><head>
//        <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
//        <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
//        <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
//        <style>body { margin: 0; padding: 0; } #map { height: 100vh; width: 100vw; background-color: #e5e3df; }</style>
//    </head>
//    <body><div id='map'></div><script>";

//            // Tọa độ trung tâm bản đồ
//            string mapInit = "var map = L.map('map').setView([22.2920807, 73.222102], 14); " +
//                             "L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);";

//            // Vẽ Pin (Marker) và Vòng tròn (Circle) theo đúng tọa độ của bạn
//            string markersAndCircles = @"
//                L.marker([22.237816, 73.217731]).addTo(map).bindPopup('Location 1');
//                L.marker([22.2792358, 73.1894696]).addTo(map).bindPopup('Location 2');
//                L.marker([22.2811848, 73.2004881]).addTo(map).bindPopup('Location 3');

//                // Vòng tròn Geofence (Bán kính tính bằng mét)
//                L.circle([22.2920807, 73.222102], {
//                    color: 'white',
//                    weight: 4,
//                    fillColor: 'green',
//                    fillOpacity: 0.5,
//                    radius: 2000 
//                }).addTo(map);
//            ";

//            string htmlFooter = "</script></body></html>";

//            // Hiển thị lên màn hình
//            var htmlSource = new HtmlWebViewSource();
//            htmlSource.Html = htmlHeader + mapInit + markersAndCircles + htmlFooter;
//            MapWebView.Source = htmlSource;
//        }

//        private async void OnBackClicked(object sender, EventArgs e)
//        {
//            try
//            {
//                // Kiểm tra xem có trang Modal nào đang mở không
//                if (Navigation.ModalStack.Count > 0)
//                {
//                    await Navigation.PopModalAsync();
//                }
//                else
//                {
//                    await Navigation.PopAsync(); // Nếu không, dùng lệnh đóng bình thường
//                }
//            }
//            catch (Exception)
//            {
//                // Backup an toàn nhất: Ép nó về lại trang chủ nếu Shell bị lỗi
//                Application.Current.MainPage = new AppShell();
//            }
//        }
//    }
//}
////using Microsoft.Maui.Controls.Maps;
////using Microsoft.Maui.Maps;

////namespace AppThaoCamVien.Pages;

////public partial class MapPage : ContentPage
////{
////    public MapPage()
////    {
////        InitializeComponent();

////        // Cài đặt vị trí mặc định khi mở bản đồ là Thảo Cầm Viên

////    }

////    //private async void OnBackClicked(object sender, EventArgs e)
////    //{
////    //    await Navigation.PopModalAsync(); // Đóng trang bản đồ
////    //}
////}

using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace AppThaoCamVien.Pages;

public partial class MapPage : ContentPage
{
    public MapPage()
    {
        InitializeComponent();

        var location = new Location(10.7870, 106.7055);

        var span = new MapSpan(location, 0.01, 0.01);

        ZooMap.MoveToRegion(span);

        var pin = new Pin
        {
            Label = "Thảo Cầm Viên",
            Location = location,
            Type = PinType.Place
        };

        ZooMap.Pins.Add(pin);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}