namespace AppThaoCamVien.Services
{
    public static class LanguageManager
    {
        public static readonly List<(string Code, string Flag, string Label)> Languages =
        [
            ("vi", "🇻🇳", "VI"), ("en", "🇬🇧", "EN"), 
        ];

        public static void Load() => Apply(Preferences.Get("AppLang", "vi"));
        public static string Current => Preferences.Get("AppLang", "vi");

        public static void Apply(string lang)
        {
            Preferences.Set("AppLang", lang);
            var r = Application.Current?.Resources;
            if (r == null) return;
            // Giao diện chính: ja dùng bản en làm nền (có thể bổ sung bản dịch đầy đủ sau)
            var mainLang = lang == "ja" ? "en" : lang;
            foreach (var kv in Strings(mainLang)) r[kv.Key] = kv.Value;
            foreach (var kv in OnboardingStringCatalog.ForLang(lang)) r[kv.Key] = kv.Value;
        }

        private static Dictionary<string, string> Strings(string lang) => lang switch
        {
            "en" => new()
            {
                // Tabs
                ["TabHome"] = "Home",
                ["TabQR"] = "Scan QR",
                ["TabNumpad"] = "Code",
                ["TabStory"] = "Animals",
                ["TabMap"] = "Map",
                ["TabAbout"] = "About",
                // Home
                ["TxtHi"] = "Welcome to Thảo Cầm Viên Tour",
                ["TxtDiscoveries"] = "Featured Discoveries",
                ["TxtHiHi"] = "Featured Discoveries",
                ["TxtZooName"] = "SAIGON ZOO & BOTANICAL GARDENS",
                ["TxtAudioGuide"] = "AUDIO GUIDE TOUR",
                ["TxtHeroTitle"] = "The Sound Journey",
                ["TxtHeroAddress"] = "2 Nguyen Binh Khiem, District 1",
                ["TxtStartTour"] = "START YOUR JOURNEY  ▶",
                ["TxtOpenHours"] = "🕐 Opening Hours",
                ["TxtHoursDetail"] = "7:00 - 18:30",
                ["TxtHoursNote"] = "Open every day",
                ["TxtTicketTitle"] = "🎟️ Entrance Fee",
                ["TxtTicketFree"] = "Under 1m: Free",
                ["TxtTicketChild"] = "1m - 1.3m: 40,000₫",
                ["TxtTicketAdult"] = "Over 1.3m: 60,000₫",
                ["TxtChooseTour"] = "CHOOSE YOUR TOUR",
                ["TxtViewAll"] = "View all",
                ["TxtTour1Title"] = "Asian Elephant Kingdom",
                ["TxtTour2Title"] = "King of the Jungle",
                ["TxtTour3Title"] = "The African Giant",
                ["TxtListenNow"] = "🔊 LISTEN NOW",
                ["TxtVoice"] = "Voice",
                ["TxtLyrics"] = "Lyrics",
                ["TxtHappeningNow"] = "Happening now",
                ["TxtRecommendedForYou"] = "Recommended for you",
                ["TxtWeatherAtZoo"] = "Weather at the zoo",
                // Animals
                ["TxtAnimalKingdom"] = "🌿 Animal Kingdom",
                ["TxtAnimalDesc"] = "Discover the amazing world of wildlife at Saigon Zoo",
                ["TxtSearchPlaceholder"] = "Search animals...",
                // Story
                ["TxtAutomatic"]="Automatic Narration",
                ["TxtInfo"] = "Info",
                ["TxtIntroduce"] = "Introduce",
                ["TxtImages"] = "Gallery",
                ["TxtMap"] = "Map",
                ["TxtEnclosure"] = "Enclosure Location",
                // Numpad
                ["TxtNumpadTitle"] = "Enter Code",
                ["TxtNumpadInstruction"] = "Enter the code on the sign",
                // QR
                ["TxtQrTitle"] = "Scan at enclosure",
                ["TxtQrInstruction"] = "Point camera at QR code",
                ["TxtQrSearching"] = "Searching for QR code...",
                ["TxtFlash"] = "Flash",
                ["TxtManualCode"] = "Enter code",
                // Map
                ["TxtMapTitle"] = "🗺️ Zoo Map",
                ["TxtAttractions"] = "Attractions",
                ["TxtPoints"] = "points",
                ["TxtListenBtn"] = "🔊 Listen",
                ["TxtDirections"] = "Directions",
                ["TxtInZone"] = "● In zone",
                ["TxtApproaching"] = "○ Approaching",
                // About
                ["TxtAboutApp"] = "Introduce",
                ["TxtAppVersion"] = "Version 1.0.0",
                ["TxtAboutTitle"] = "About the app",
                ["TxtAboutDesc"] = "Smart audio guide for Saigon Zoo. GPS-based auto narration, QR scan, multilingual support.",
                ["TxtDeveloper"] = "Developed by: Dũng Nguyễn",
                ["TxtAppInfo"] = "App Information",
            },
            
            _ => new() // tiếng Việt
            {
                ["TabHome"] = "Trang chủ",
                ["TabQR"] = "Mã QR",
                ["TabNumpad"] = "Nhập Số",
                ["TabStory"] = "Câu chuyện",
                ["TabMap"] = "Sơ đồ",
                ["TabAbout"] = "Giới thiệu",
                ["TxtHi"] = "Chào mừng tới Thảo Cầm Viên Tour",
                ["TxtDiscoveries"] = "Chào mừng tới Thảo Cầm Viên Tour",
                ["TxtZooName"] = "THẢO CẦM VIÊN SÀI GÒN",
                ["TxtAudioGuide"] = "AUDIO GUIDE TOUR",
                ["TxtHeroTitle"] = "Hành Trình Âm Thanh",
                ["TxtHeroAddress"] = "2 Nguyễn Bỉnh Khiêm, Quận 1",
                ["TxtStartTour"] = "BẮT ĐẦU CHUYẾN ĐI  ▶",
                ["TxtOpenHours"] = "🕐 Giờ mở cửa",
                ["TxtHoursDetail"] = "7:00 - 18:30",
                ["TxtHoursNote"] = "Mở cửa tất cả các ngày",
                ["TxtTicketTitle"] = "🎟️ Vé vào cổng",
                ["TxtTicketFree"] = "Dưới 1m: Miễn phí",
                ["TxtTicketChild"] = "1m - 1m3: 40.000đ",
                ["TxtTicketAdult"] = "Trên 1m3: 60.000đ",
                ["TxtChooseTour"] = "CHỌN TOUR CỦA BẠN",
                ["TxtViewAll"] = "Xem tất cả",
                ["TxtTour1Title"] = "Vương Quốc Voi Châu Á",
                ["TxtTour2Title"] = "Chúa Tể Sơn Lâm",
                ["TxtTour3Title"] = "Gã Khổng Lồ Châu Phi",
                ["TxtListenNow"] = "🔊 NGHE NGAY",
                ["TxtVoice"] = "Giọng đọc",
                ["TxtLyrics"] = "Lời thoại",
                ["TxtHappeningNow"] = "Đang diễn ra ngay lúc này",
                ["TxtRecommendedForYou"] = "Gợi ý cho bạn",
                ["TxtWeatherAtZoo"] = "Thời tiết tại Thảo Cầm Viên",
                ["TxtAnimalKingdom"] = "🌿 Vương quốc động vật",
                ["TxtAnimalDesc"] = "Khám phá thế giới muôn loài tuyệt đẹp tại Thảo Cầm Viên",
                ["TxtSearchPlaceholder"] = "Tìm kiếm động vật...",
                ["TxtInfo"] = "Thông tin",
                ["TxtImages"] = "Hình ảnh",
                ["TxtMap"] = "Bản đồ",
                ["TxtIntroduce"] = "Giới thiệu",
                ["TxtEnclosure"] = "Vị trí chuồng nuôi",
                ["TxtNumpadTitle"] = "Nhập mã số",
                ["TxtNumpadInstruction"] = "Nhập mã trên biển báo",
                ["TxtQrTitle"] = "Quét mã tại chuồng thú",
                ["TxtQrInstruction"] = "Hướng camera vào mã QR",
                ["TxtQrSearching"] = "Đang tìm kiếm mã QR...",
                ["TxtFlash"] = "Bật đèn",
                ["TxtManualCode"] = "Nhập mã tay",
                ["TxtMapTitle"] = "🗺️ Bản đồ Thảo Cầm Viên",
                ["TxtAttractions"] = "Các điểm tham quan",
                ["TxtPoints"] = "điểm",
                ["TxtAutomatic"] = "Thuyết minh tự động",
                ["TxtListenBtn"] = "🔊 Nghe",
                ["TxtDirections"] = "Chỉ đường đến điểm này",
                ["TxtInZone"] = "● Trong vùng",
                ["TxtApproaching"] = "○ Đang tiếp cận",
                ["TxtAboutApp"] = "Giới thiệu",
                ["TxtAppVersion"] = "Phiên bản 1.0.0",
                ["TxtAboutTitle"] = "Về ứng dụng",
                ["TxtAboutDesc"] = "Ứng dụng hướng dẫn tham quan Thảo Cầm Viên thông minh. GPS tự động, QR code, đa ngôn ngữ.",
                ["TxtDeveloper"] = "Phát triển bởi: Dũng Nguyễn",
                ["TxtAppInfo"] = "Thông tin ứng dụng",
            }
        };
    }
}
