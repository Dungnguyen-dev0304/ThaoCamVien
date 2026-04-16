🦁 Thảo Cầm Viên Tour — Hệ Thống Hướng Dẫn Tham Quan Thông Minh

Nền tảng đa phương tiện (Multi-platform) hỗ trợ du khách trải nghiệm Thảo Cầm Viên Sài Gòn thông qua Audio Guide, GPS Tracking, QR Code và bản đồ tương tác — hoạt động cả khi không có Internet.


Mục lục

Tổng quan kiến trúc
Technology Stack
Tính năng hệ thống — Problem → Solution
Database & System Design
Cài đặt & Triển khai
API Documentation
Quality Assurance
Cấu trúc thư mục
Thông tin tác giả


Tổng quan kiến trúc
Dự án được thiết kế theo kiến trúc Multi-tier, Multi-platform với nguyên tắc Separation of Concerns: mỗi tầng (layer) chỉ chịu trách nhiệm cho một nhóm chức năng duy nhất, giảm thiểu coupling và tối đa hoá khả năng mở rộng.
┌─────────────────────────────────────────────────────┐
│                   CLIENT LAYER                      │
│  ┌──────────────────┐    ┌───────────────────────┐  │
│  │  Mobile App       │    │  Web Admin Portal     │  │
│  │  .NET MAUI        │    │  ASP.NET Core MVC     │  │
│  │  (Android/iOS)    │    │  (Quản trị nội dung)  │  │
│  └────────┬─────────┘    └──────────┬────────────┘  │
│           │     RESTful API (JSON)  │               │
├───────────┼─────────────────────────┼───────────────┤
│           ▼                         ▼               │
│  ┌──────────────────────────────────────────────┐   │
│  │            API LAYER                          │   │
│  │     ASP.NET Core Web API (.NET 10)           │   │
│  │     Controllers → Services → DbContext       │   │
│  └──────────────────────┬───────────────────────┘   │
│                         │  Entity Framework Core    │
├─────────────────────────┼───────────────────────────┤
│           ┌─────────────▼──────────────┐            │
│           │       DATA LAYER           │            │
│           │   SQL Server (Production)  │            │
│           │   SQLite    (Offline App)  │            │
│           └────────────────────────────┘            │
├─────────────────────────────────────────────────────┤
│  SHARED LAYER: Entity Models & DTOs dùng chung     │
│  giữa API, Web và Mobile — đảm bảo Single Source   │
│  of Truth cho toàn bộ hệ thống.                    │
└─────────────────────────────────────────────────────┘
Tại sao lại chia thành 4 project riêng biệt? Để bất kỳ thành viên nào trong nhóm cũng có thể phát triển song song (parallel development) mà không gây conflict. Shared Library đóng vai trò "hợp đồng" (contract) giữa các tầng — khi model thay đổi, tất cả các project đều đồng bộ tự động.

Technology Stack
Phân tầng theo chức năng
TầngCông nghệPhiên bảnVai tròMobile.NET MAUI10.0Cross-platform (Android/iOS/macOS/Windows)Web AdminASP.NET Core MVC10.0Portal quản trị cho nhân viênAPIASP.NET Core Web API10.0RESTful backend xử lý business logicORMEntity Framework Core10.0.6Object-Relational Mapping, Code-First MigrationsDatabaseSQL Server + SQLite—SQL Server cho server, SQLite cho offline mobileShared.NET Class Library10.0Entity Models & DTOs dùng chung
Thư viện quan trọng — Mobile App
Thư việnMục đíchLý do chọnMapsui 5.0.2Bản đồ tương tác với OpenStreetMap tilesHoạt động offline, không phụ thuộc Google API keyZXing.Net.Maui 0.7.4Quét QR Code realtimeThư viện mã nguồn mở phổ biến nhất cho barcode/QR trên .NETPlugin.Maui.Audio 4.0.0Phát audio thuyết minhHỗ trợ stream từ HTTP và local filePolly 8.6.6Resilience patterns (Retry, Circuit Breaker, Timeout)Đảm bảo app không crash khi mất mạngCommunityToolkit.Maui 14.1.0UI components và behaviorsBổ sung các control thiếu trong MAUI coreGTranslate 2.3.1Hỗ trợ dịch tự độngFallback khi chưa có bản dịch thủ côngBCrypt.Net-Next 4.1.0Hash mật khẩuChuẩn bảo mật công nghiệp, chống brute-forcesqlite-net-pcl 1.9.172Local database trên thiết bịCache dữ liệu cho chế độ offline

Tính năng hệ thống — Problem → Solution
1. Audio Guide đa ngôn ngữ

Problem: Du khách quốc tế (Anh, Thái, v.v.) không thể đọc bảng hướng dẫn tiếng Việt. Thuê hướng dẫn viên riêng quá tốn kém.

Solution: Hệ thống cung cấp file audio thuyết minh cho từng điểm tham quan (POI) với nhiều ngôn ngữ. Khi audio cho ngôn ngữ được yêu cầu chưa tồn tại, hệ thống tự động fallback về tiếng Việt. Nếu không có file audio nào, Text-to-Speech engine sẽ đọc nội dung mô tả — đảm bảo du khách luôn nhận được thông tin.
Yêu cầu audio (EN) → Tìm file EN → Không có → Fallback (VI) → Không có → TTS Engine

TTS hỗ trợ: vi, en, th, id, ms, km
Audio stream qua HTTP, tải về MemoryStream trước khi phát (tránh lỗi ObjectDisposedException khi NetworkStream bị đóng giữa chừng)

2. Quét QR Code & Nhập số thủ công

Problem: Du khách muốn tra cứu nhanh thông tin một con vật/cây cảnh ngay tại chỗ mà không phải cuộn qua danh sách dài.

Solution: Mỗi POI gắn một mã QR theo format TCV-{id}. App quét QR bằng camera (ZXing.Net.Maui) và resolve về POI tương ứng. Đối với du khách lớn tuổi hoặc thiết bị không hỗ trợ camera, trang NumpadPage cho phép nhập số POI thủ công qua bàn phím số.

Format QR hỗ trợ: TCV-1, TCV-001, TCV1, hoặc chỉ 1
Admin portal tích hợp trang in QR hàng loạt (sử dụng QRCode.js)

3. Bản đồ tương tác & Geofencing

Problem: Thảo Cầm Viên rộng hơn 20 hecta. Du khách thường bị lạc hoặc bỏ lỡ những điểm đáng xem.

Solution: Bản đồ OpenStreetMap được render offline qua Mapsui, hiển thị vị trí thực của du khách cùng tất cả POI pin (phân biệt bằng màu theo category). GeofencingEngine tự động phát hiện khi du khách đến gần một POI (dựa trên công thức Haversine) và kích hoạt thuyết minh audio.
GPS cập nhật → Haversine distance < POI radius (30m mặc định)
  → Debounce (3 giây) → Cooldown check (5 phút)
    → NarrationEngine phát audio → Ghi lịch sử tham quan

Boundary polygon 23 toạ độ xác định ranh giới Thảo Cầm Viên
Location debouncing: chỉ cập nhật khi di chuyển > 5 mét (tiết kiệm pin)

4. Chế độ Offline

Problem: Khu vực Thảo Cầm Viên WiFi không ổn định. Du khách quốc tế thường không có SIM data Việt Nam.

Solution: OfflineBundleDownloadService cho phép tải trước toàn bộ dữ liệu POI, tour, audio và hình ảnh qua WiFi tại khách sạn. Dữ liệu được cache vào SQLite local. Bản đồ OpenStreetMap cũng hoạt động offline sau khi tiles đã được cache.
5. Hệ thống Tour có trình tự

Problem: Du khách lần đầu không biết nên xem gì trước, xem gì sau. Mỗi người có lượng thời gian khác nhau.

Solution: Admin tạo sẵn các tour có thứ tự (OrderIndex trong bảng tour_poi), ước tính thời gian hoàn thành. Du khách chọn tour phù hợp và được hướng dẫn đi theo trình tự tối ưu.
6. Web Admin Portal

Problem: Quản lý nội dung (thêm POI, upload audio, dịch nội dung) cần giao diện trực quan, không thể yêu cầu nhân viên thao tác database trực tiếp.

Solution: ASP.NET Core MVC portal với authentication dựa trên Cookie (hết hạn 7 ngày). Phân quyền Role-based: Admin (role = 0) quản lý toàn bộ, User (role = 1) chỉ xem.

CRUD đầy đủ cho POI, Tour, Audio, QR Code, Translation
Upload audio (MP3) theo từng ngôn ngữ
Quản lý bản dịch đa ngôn ngữ cho Name & Description
Trang in QR hàng loạt để dán tại các chuồng/khu vực


Database & System Design
Entity Relationship
Hệ thống sử dụng 11 bảng chính với quan hệ được ràng buộc bởi Foreign Key và Cascading Rules:
┌──────────────┐       ┌───────────────┐
│ poi_category │──1:N──│     pois      │──1:N──┬── poi_translations
│              │       │               │       ├── poi_media
└──────────────┘       │  (core table) │       ├── poi_audios
                       │               │       ├── qr_codes
                       └───────┬───────┘       └── poi_visit_history
                               │
                          N:M (qua tour_poi)
                               │
                       ┌───────┴───────┐
                       │     tour      │
                       │ (OrderIndex)  │
                       └───────────────┘

┌──────────────┐
│    users     │──1:N──┬── poi_visit_history
│ (BCrypt pwd) │       └── user_location_log
└──────────────┘
Tính toàn vẹn dữ liệu (Data Integrity)
Cơ chếÁp dụngMục đíchForeign Key ConstraintsTất cả bảng conĐảm bảo không tồn tại orphan records (VD: audio không thuộc POI nào)Cascading DeletePoiTranslationsKhi xoá POI, tất cả bản dịch tự động bị xoá — tránh dữ liệu rácDefault Valuescreated_at = GETDATE()Timestamp tự động, tránh lỗi khi client không gửiPrecision Constraintsdecimal(11,8) cho lat/lng8 chữ số thập phân ≈ độ chính xác 1.1mm — đủ cho GPS tracking trong khuôn viênColumn Naming Conventionsnake_case (DB) ↔ PascalCase (C#)Tuân thủ convention của từng nền tảng, EF Core tự mapBCrypt Password HashingBảng usersMật khẩu được hash bằng BCrypt — không lưu plaintext, chống rainbow table attackRole-based AuthorizationRole column (0/1)Phân quyền tại database level, không chỉ application level
Migration History
Dự án sử dụng EF Core Code-First Migrations với 16 phiên bản migration, cho thấy quá trình phát triển iterative:
InitialThaoCamVienStructure → SyncFromFriendCode → UpdateMoi → AddDescriptionToTable
→ AddPoiTranslations → TenMoi → Audio → AddIsActiveToTour → UpDateDB
→ AddPoiAudio → UpdatePasswordColumnLength → MigrationMoi → AddRoleToUser
→ AddAuidoCodeInPoi → AddColumnAudioCode

Cài đặt & Triển khai
Prerequisites
Công cụPhiên bản tối thiểuGhi chú.NET SDK10.0+Bao gồm MAUI workloadVisual Studio2022 17.x+Cần cài workload: ASP.NET, .NET MAUISQL Server2019+Hoặc SQL Server Express / LocalDBAndroid SDKAPI 21+ (Lollipop)Cho phát triển/test mobileNode.js(tuỳ chọn)Nếu cần chạy frontend tools
Bước 1: Clone repository
bashgit clone <repository-url>
cd ThaoCamVienTour
Bước 2: Cấu hình Database
2a. Tạo database và import schema:
sql-- Mở SQL Server Management Studio, tạo database mới tên "web"
CREATE DATABASE web;
GO
Sau đó chạy file web.sql tại root project để khởi tạo toàn bộ bảng và dữ liệu mẫu (23 POI, categories, QR codes).
2b. Kiểm tra Connection String:
File ApiThaoCamVien/appsettings.json:
json{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=web;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}

Nếu dùng SQL Server Authentication, thay bằng: Server=.;Database=web;User Id=sa;Password=<mật_khẩu>;TrustServerCertificate=True;

2c. Áp dụng Migrations (nếu chưa import SQL):
bashcd ApiThaoCamVien
dotnet ef database update
Bước 3: Chạy API
bashcd ApiThaoCamVien
dotnet run
API sẽ chạy tại http://localhost:5xxx. Truy cập Swagger UI tại /swagger để xem tài liệu API tương tác.
Bước 4: Chạy Web Admin Portal
bashcd WebThaoCamVien
dotnet run
Đăng nhập bằng tài khoản admin đã tạo trong database.
Bước 5: Chạy Mobile App
bashcd AppThaoCamVien
dotnet build -t:Run -f net10.0-android
Lưu ý quan trọng cho Android Emulator:

API URL tự động detect: 10.0.2.2 (emulator) hoặc 192.168.x.x (thiết bị thật)
App có trang ApiConfigPage trong Onboarding để cấu hình IP thủ công
HTTPS redirection đã được tắt trong API để tương thích với emulator

Bước 6: Upload nội dung

Truy cập Web Admin → Đăng nhập
Thêm/chỉnh sửa POI (tên, toạ độ, category, hình ảnh)
Upload file audio MP3 cho từng POI (chọn ngôn ngữ: vi/en/th)
Tạo bản dịch cho Name & Description
In QR codes và dán tại các khu vực trong Thảo Cầm Viên


API Documentation
Base URL
http://localhost:{port}/api
Endpoints chính
POI (Points of Interest)
MethodEndpointMô tảQuery ParamsGET/api/PoisLấy tất cả POI đang hoạt động?lang=vi|en|thGET/api/Pois/{id}Chi tiết một POI—GET/api/Pois/qr/{qrData}Resolve QR code thành POI—GET/api/Pois/numpad/{poiId}Tra cứu POI bằng mã số—GET/api/Pois/healthHealth check—
Tour
MethodEndpointMô tảGET/api/ToursDanh sách tour (kèm số lượng POI)GET/api/Tours/{id}Chi tiết tour (danh sách POI theo thứ tự)
Audio
MethodEndpointMô tảGET/api/Audio/{poiId}Tất cả audio của POI (mọi ngôn ngữ)GET/api/Audio/{poiId}/{lang}Audio theo ngôn ngữ (fallback sang vi)
Map
MethodEndpointMô tảGET/api/map/boundaryPolygon ranh giới Thảo Cầm Viên (23 toạ độ)GET/api/map/poisToạ độ tất cả POI cho rendering bản đồ
Mobile (endpoints tối ưu cho app)
MethodEndpointMô tảGET/api/mobile/home-feedDữ liệu trang chủ (greeting, cards, sections)GET/api/mobile/pois/nearbyPOI gần nhất (tính bằng Haversine)GET/api/mobile/animalsDanh sách động vật (có filter theo category)POST/api/mobile/qr/lookupResolve mã QRGET/api/mobile/about/sectionsNội dung trang giới thiệu
Response Format (ví dụ)
json// GET /api/Audio/1/en
{
  "audioId": 5,
  "poiId": 1,
  "languageCode": "en",
  "fileName": "tiger_en.mp3",
  "streamUrl": "/audio/pois/1_en.mp3",
  "fileSizeBytes": 245760,
  "durationSeconds": 45
}
Xử lý lỗi API
HTTP StatusÝ nghĩaKhi nào xảy ra200 OKThành côngRequest hợp lệ, có dữ liệu trả về404 Not FoundKhông tìm thấyPOI/Tour/Audio không tồn tại500 Internal Server ErrorLỗi serverException chưa xử lý (kèm message mô tả)

Quality Assurance
Resilience & Exception Handling
Dự án áp dụng Polly Resilience Pipeline trên mobile app để đảm bảo trải nghiệm mượt mà ngay cả khi mạng không ổn định:
Request → Timeout (15s) → Retry (2 lần, exponential backoff) → Circuit Breaker → Response
PatternCấu hìnhTại saoTimeout15 giâyTránh app treo vô thời hạn khi server không phản hồiRetry2 lần, backoff mũXử lý lỗi tạm thời (network glitch) mà không spam serverCircuit BreakerMở sau N lỗi liên tiếpKhi server chắc chắn đã down, ngừng gửi request để tiết kiệm tài nguyên
Nguyên tắc xử lý lỗi trong Service layer: Không bao giờ throw exception ra ngoài. Mọi service method đều wrap trong try-catch, trả về null hoặc empty collection khi lỗi — UI layer chịu trách nhiệm hiển thị trạng thái phù hợp.
Thread Safety
Cơ chếÁp dụng tạiMục đíchSemaphoreSlimNarrationEngineNgăn chặn nhiều audio phát đồng thời khi geofencing trigger liên tụcDebouncing (3s)GeofencingEngineTránh spam trigger khi GPS dao độngCooldown (5 phút)NarrationEngineKhông phát lại cùng một POI trong 5 phútMovement threshold (5m)LocationServiceChỉ cập nhật vị trí khi thực sự di chuyển — tiết kiệm pin
UI/UX Optimization
Kỹ thuậtVị tríHiệu quảSkeleton LoadingSkeletonBlock controlHiển thị placeholder hình xương khi chờ dữ liệu — thay vì màn hình trắngStateContainerTất cả trang chínhQuản lý 3 trạng thái: Loading, Error, Empty — tránh UX rỗng hoặc crashGradient & Visual DesignHomePage, StoryAudioPageTạo cảm giác chuyên nghiệp và thân thiện với du kháchOnboarding Flow4 trang (Welcome → Permissions → Download → Config)Hướng dẫn du khách từng bước: cấp quyền GPS/Camera, tải offline, cấu hình server

Cấu trúc thư mục
ThaoCamVienTour/
├── ApiThaoCamVien/              # ASP.NET Core Web API
│   ├── Controllers/             # 8 controller xử lý HTTP endpoints
│   │   ├── PoisController.cs    #   POI CRUD + QR resolve + Numpad
│   │   ├── ToursController.cs   #   Tour listing & detail
│   │   ├── AudioController.cs   #   Audio streaming & metadata
│   │   ├── MapController.cs     #   Boundary polygon & POI locations
│   │   ├── MobileController.cs  #   Endpoints tối ưu cho mobile
│   │   ├── QrCodesController.cs #   QR code management + print
│   │   ├── AccountController.cs #   Authentication
│   │   └── PoiExtController.cs  #   Extended POI operations
│   ├── Models/
│   │   ├── WebContext.cs         # EF Core DbContext (11 entities)
│   │   └── VisitRequest.cs      # Request DTOs
│   ├── Migrations/               # 16 migration files (Code-First)
│   ├── Scripts/                  # SQL seed scripts (translations)
│   ├── Program.cs                # Middleware pipeline & DI config
│   └── appsettings.json          # Connection string & logging
│
├── AppThaoCamVien/              # .NET MAUI Mobile App
│   ├── Pages/                   # 12 XAML pages
│   │   ├── HomePage.xaml         #   Trang chủ (feed, nearby, mini player)
│   │   ├── MapPage.xaml          #   Bản đồ tương tác + GPS
│   │   ├── QrPage.xaml           #   Quét QR Code
│   │   ├── NumpadPage.xaml       #   Nhập mã số POI
│   │   ├── StoryAudioPage.xaml   #   Chi tiết POI + audio player
│   │   ├── AnimalsPage.xaml      #   Danh sách động vật
│   │   └── Onboarding/          #   4 trang hướng dẫn ban đầu
│   ├── ViewModels/               # MVVM ViewModels
│   ├── Services/                 # 12 service classes
│   │   ├── ApiService.cs         #   HTTP client + Polly resilience
│   │   ├── AudioService.cs       #   Audio playback engine
│   │   ├── LocationService.cs    #   GPS tracking & permissions
│   │   ├── GeofencingEngine.cs   #   Haversine proximity detection
│   │   ├── NarrationEngine.cs    #   Orchestrator: Audio + TTS
│   │   ├── TtsEngine.cs          #   Text-to-Speech (6 ngôn ngữ)
│   │   ├── DatabaseService.cs    #   SQLite local cache
│   │   ├── LanguageManager.cs    #   i18n management
│   │   └── OfflineBundleDownloadService.cs
│   └── Controls/                 # Custom UI controls
│       ├── SkeletonBlock.xaml    #   Skeleton loading placeholder
│       └── StateContainer.xaml   #   Loading/Error/Empty states
│
├── WebThaoCamVien/              # ASP.NET Core MVC Admin Portal
│   ├── Controllers/             # 6 controller (Admin, Account, Audio, Tour, Translation, Home)
│   └── Views/                   # Razor views cho CRUD operations
│
├── SharedThaoCamVien/           # Shared Class Library
│   └── Models/                  # 12 entity models dùng chung
│       ├── Poi.cs, PoiCategory.cs, PoiAudio.cs
│       ├── PoiTranslation.cs, PoiMedium.cs
│       ├── Tour.cs, TourPoi.cs
│       ├── QrCode.cs
│       ├── User.cs, UserLocationLog.cs
│       └── PoiVisitHistory.cs
│
├── web.sql                      # SQL Server schema + seed data
├── ThaoCamVien.slnx             # Solution file
└── README.md

Thông tin tác giả
Đồ ánĐồ án tốt nghiệpChủ đềHệ thống hướng dẫn tham quan Thảo Cầm Viên Sài GònNền tảng.NET 10.0 (API + MAUI + MVC)DatabaseSQL Server + SQLite


"Công nghệ tốt nhất là công nghệ mà du khách không nhận ra mình đang dùng — họ chỉ biết rằng chuyến tham quan hôm nay thú vị hơn mọi khi."
