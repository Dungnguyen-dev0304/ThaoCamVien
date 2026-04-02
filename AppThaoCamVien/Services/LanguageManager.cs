using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services
{
    public static class LanguageManager
    {
        public static void LoadCurrentLanguage()
        {
            string lang = Preferences.Get("AppLang", "vi");
            SetLanguage(lang);
        }

        public static void SetLanguage(string lang)
        {
            var res = Application.Current.Resources;

            switch (lang)
            {
                case "en":
                    res["TabHome"] = "Home"; res["TabQR"] = "Scan QR"; res["TabNumpad"] = "Code";
                    res["TabStory"] = "Animals"; res["TabMap"] = "Map"; res["TabAbout"] = "About";
                    res["TxtAppInfo"] = "App Information";
                    // Thêm các từ vựng trang chủ tại đây...
                    break;
                case "th":
                    res["TabHome"] = "หน้าแรก"; res["TabQR"] = "สแกน QR"; res["TabNumpad"] = "รหัส";
                    res["TabStory"] = "สัตว์"; res["TabMap"] = "แผนที่"; res["TabAbout"] = "เกี่ยวกับ";
                    res["TxtAppInfo"] = "ข้อมูลแอป";
                    break;
                case "id":
                    res["TabHome"] = "Beranda"; res["TabQR"] = "Pindai QR"; res["TabNumpad"] = "Kode";
                    res["TabStory"] = "Hewan"; res["TabMap"] = "Peta"; res["TabAbout"] = "Tentang";
                    res["TxtAppInfo"] = "Info Aplikasi";
                    break;
                case "ms":
                    res["TabHome"] = "Utama"; res["TabQR"] = "Imbas QR"; res["TabNumpad"] = "Kod";
                    res["TabStory"] = "Haiwan"; res["TabMap"] = "Peta"; res["TabAbout"] = "Tentang";
                    res["TxtAppInfo"] = "Maklumat Aplikasi";
                    break;
                case "km":
                    res["TabHome"] = "ទំព័រដើម"; res["TabQR"] = "ស្កេន QR"; res["TabNumpad"] = "លេខកូដ";
                    res["TabStory"] = "សត្វ"; res["TabMap"] = "ផែនទី"; res["TabAbout"] = "អំពី";
                    res["TxtAppInfo"] = "ព័ត៌មានកម្មវិធី";
                    break;
                default: // "vi"
                    res["TabHome"] = "Trang chủ"; res["TabQR"] = "Mã QR"; res["TabNumpad"] = "Nhập Số";
                    res["TabStory"] = "Câu chuyện"; res["TabMap"] = "Sơ đồ"; res["TabAbout"] = "Giới thiệu";
                    res["TxtAppInfo"] = "Thông tin ứng dụng";
                    break;
            }
        }
    }
}