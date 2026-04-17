# TÀI LIỆU CHUẨN BỊ BẢO VỆ ĐỒ ÁN — THẢO CẦM VIÊN TOUR

---

## PHẦN 1: TỔNG QUAN DỰ ÁN

### Dự án là gì?

Hệ thống hướng dẫn tham quan Thảo Cầm Viên Sài Gòn, gồm 3 ứng dụng liên thông:

- **WebThaoCamVien** (ASP.NET Core MVC): Portal quản trị cho nhân viên — CRUD POI, Tour, Audio, Bản dịch, Dashboard thống kê.
- **AppThaoCamVien** (.NET MAUI): App di động cho du khách — Bản đồ, Audio Guide, QR Code, Geofencing tự động phát thuyết minh.
- **ApiThaoCamVien** (ASP.NET Core Web API): Backend RESTful kết nối Web và App.
- **SharedThaoCamVien**: Class Library chứa Entity Models dùng chung — đảm bảo Single Source of Truth.

### Kiến trúc tổng thể

```
App (MAUI) ──HTTP/JSON──→ API (ASP.NET Core) ←──HTTP/JSON── Web Admin (MVC)
                              │
                     Entity Framework Core
                              │
                         SQL Server
```

Tại sao tách 4 project? Để phát triển song song (parallel development), giảm coupling. SharedLibrary là "hợp đồng" giữa các tầng.

---

## PHẦN 2: THỐNG KÊ SỐ LIỆU

### Tổng số chức năng

| Nhóm | Mã | Số lượng |
|------|-----|---------|
| **Web — Yêu cầu chức năng** | FR-01 → FR-07 | **7** |
| **App — Yêu cầu chức năng** | AFR-01 → AFR-07 | **7** |
| **Web — Use Case** | WUC-01 → WUC-17 | **17** |
| **App — Use Case** | AUC-01 → AUC-10 | **10** |
| **TỔNG USE CASE** | | **27** |

### Chi tiết 27 Use Case

**Web (17 use case):**

| Mã | Chức năng |
|----|-----------|
| WUC-01 | Đăng nhập hệ thống |
| WUC-02 | Liệt kê POI |
| WUC-03 | Tạo POI mới |
| WUC-04 | Sửa POI |
| WUC-05 | Xóa/Disable POI |
| WUC-06 | Upload/Quản lý audio theo POI + ngôn ngữ |
| WUC-07 | Sửa/Xóa audio |
| WUC-08 | Tạo/Sửa bản dịch cho POI |
| WUC-09 | Tạo/Sửa bản dịch cho Tour |
| WUC-10 | Xem action log (CRUD/auth) |
| WUC-11 | Xem log phát POI từ App (analytics) |
| WUC-12 | Tạo/Sửa/Xóa tour |
| WUC-13 | Sắp xếp POI trong tour |
| WUC-14 | Xuất tour (CSV/PDF) hoặc xuất sang App |
| WUC-15 | Quản lý tài khoản |
| WUC-16 | Phân quyền chi tiết (RBAC) |
| WUC-17 | Xem dashboard chung |

**App (10 use case):**

| Mã | Chức năng |
|----|-----------|
| AUC-01 | Lấy vị trí hiện tại |
| AUC-02 | Tính khoảng cách tới POI (Haversine) |
| AUC-03 | Kích hoạt POI khi vào vùng (Geofencing) |
| AUC-04 | Phát thuyết minh theo POI |
| AUC-05 | Xem bản đồ và marker POI |
| AUC-06 | Tap POI → xem chi tiết → phát lại |
| AUC-07 | Cài đặt (ngôn ngữ, TTS, GPS) |
| AUC-08 | Tải gói nội dung offline |
| AUC-09 | Ghi log analytics ẩn danh |
| AUC-10 | Tra cứu QR (scan + nhập tay) |

### Tổng số Sequence Diagrams

| Nhóm | Số lượng | Chi tiết |
|------|---------|----------|
| **Web Sequence** | **18** | 01-account-login → 18-translation-save-update |
| **App Sequence** | **9** | 01-app-home-flow → 09-app-map-geofence-narration-flow |
| **TỔNG** | **27** |

**Nguyên tắc: Mỗi use case có ít nhất 1 sequence diagram tương ứng.**

### Chi tiết 18 Web Sequence Diagrams

| # | File | Chức năng |
|---|------|-----------|
| 01 | account-login | Đăng nhập |
| 02 | account-logout | Đăng xuất |
| 03 | tour-list | Danh sách tour |
| 04 | tour-add | Thêm tour |
| 05 | tour-edit | Sửa tour |
| 06 | tour-toggle-delete | Bật/tắt & xóa tour |
| 07 | admin-poi-crud | POI CRUD tổng quan |
| 08 | audio-management | Quản lý audio tổng quan |
| 09 | translation-management | Quản lý bản dịch tổng quan |
| 10 | admin-index-analytics | Dashboard thống kê |
| 11 | admin-poi-add | Thêm POI chi tiết |
| 12 | admin-poi-edit | Sửa POI chi tiết |
| 13 | admin-poi-delete | Xóa POI |
| 14 | audio-upload | Upload audio |
| 15 | audio-save-recording | Ghi âm trên web |
| 16 | audio-delete | Xóa audio |
| 17 | translation-save-add | Thêm bản dịch |
| 18 | translation-save-update | Cập nhật bản dịch |

### Chi tiết 9 App Sequence Diagrams

| # | File | Chức năng |
|---|------|-----------|
| 01 | app-home-flow | Home (Loading/Success/Empty/Error) |
| 02 | app-map-flow | Map (Clusters + Nearby + Cache) |
| 03 | app-story-audio-flow | StoryAudio (Voices + Lyrics) |
| 04 | app-qr-lookup-flow | QR Lookup |
| 05 | app-about-sections-flow | About Sections |
| 06 | app-onboarding-flow | Onboarding (4 bước) |
| 07 | app-animals-explore-flow | Explore/Animals |
| 08 | app-numpad-lookup-flow | Numpad nhập mã POI |
| 09 | app-map-geofence-narration-flow | Geofencing + tự phát narration |

### Thuyết minh tự động — Số ngôn ngữ

**6 ngôn ngữ** được hỗ trợ bởi TTS Engine: `vi` (Việt), `en` (Anh), `th` (Thái), `id` (Indonesia), `ms` (Mã Lai), `km` (Khmer/Campuchia).

Audio file được quản lý theo 3 ngôn ngữ chính: `vi`, `en`, `th`. Các ngôn ngữ còn lại fallback qua TTS.

---

## PHẦN 3: CÂU HỎI KHÓ THẦY SẼ HỎI & CÁCH TRẢ LỜI

---

### CÂU 1: "Tại sao em chọn .NET MAUI chứ không phải Flutter hay React Native?"

**Trả lời:**

Dạ thưa thầy, em chọn .NET MAUI vì 3 lý do chính:

1. **Thống nhất tech stack**: Cả 3 project (API, Web, Mobile) đều dùng C# và .NET 10. Điều này giúp em share được Entity Models qua SharedThaoCamVien library — tránh việc phải viết lại model ở 2 ngôn ngữ khác nhau rồi bị lệch.

2. **Native performance**: MAUI compile ra native code cho từng platform (không chạy qua JavaScript bridge như React Native). Với bài toán bản đồ + GPS tracking liên tục + audio streaming, performance là yếu tố quan trọng.

3. **Chia sẻ code thực sự**: SharedThaoCamVien chứa 12 entity models, dùng chung giữa API (EF Core) và Mobile (SQLite). Nếu dùng Flutter thì em phải viết lại toàn bộ model bằng Dart, rồi maintain 2 bộ model song song — dễ lệch.

**Nếu thầy hỏi thêm "Nhược điểm?":** Em thừa nhận MAUI community nhỏ hơn Flutter, và một số thư viện bên thứ ba ít hơn. Nhưng với scope đồ án này, các thư viện cần thiết (Mapsui, ZXing, Plugin.Maui.Audio) đều hoạt động ổn định.

---

### CÂU 2: "Giải thích cơ chế Geofencing. Làm sao biết du khách đang gần con hổ?"

**Trả lời:**

Dạ, em triển khai Geofencing theo 4 bước:

**Bước 1 — GPS Tracking:** LocationService lấy toạ độ GPS liên tục. Nhưng em có áp dụng **movement threshold 5 mét** — nghĩa là nếu du khách đứng yên (GPS dao động < 5m), em không cập nhật lại → tiết kiệm pin.

**Bước 2 — Tính khoảng cách (Haversine):** Mỗi khi vị trí cập nhật, GeofencingEngine tính khoảng cách từ vị trí hiện tại tới tất cả POI bằng **công thức Haversine** — công thức tính khoảng cách giữa 2 điểm trên bề mặt cầu (Trái Đất):

```
a = sin²(Δlat/2) + cos(lat1) × cos(lat2) × sin²(Δlng/2)
d = 2R × arcsin(√a)
```

**Bước 3 — So sánh với radius:** Mỗi POI có radius (mặc định 30 mét). Khi `distance ≤ radius` → du khách đã "vào vùng" POI đó.

**Bước 4 — Debounce + Cooldown:** Không phát thuyết minh ngay. Em áp dụng:
- **Debounce 3 giây**: đợi 3 giây GPS ổn định, tránh trigger nhầm khi GPS nhảy.
- **Cooldown 5 phút**: cùng một POI không phát lại trong 5 phút, tránh du khách đi qua đi lại nghe đi nghe lại.
- **Priority**: nếu đồng thời gần nhiều POI, chọn POI có priority cao nhất + gần nhất.

**Tại sao dùng Haversine mà không dùng khoảng cách Euclid?** Vì toạ độ GPS là trên mặt cầu, không phải mặt phẳng. Ở khoảng cách ngắn (< 1km) thì sai số không lớn, nhưng em vẫn dùng Haversine cho chính xác — đây là chuẩn ngành GPS.

---

### CÂU 3: "Mất mạng giữa chừng thì app có crash không?"

**Trả lời:**

Dạ không crash ạ. Em xử lý mất mạng ở 3 tầng:

**Tầng 1 — Polly Resilience Pipeline (ApiService):**
```
Request → Timeout (15s) → Retry (2 lần, exponential backoff) → Circuit Breaker
```
- **Timeout 15 giây**: không để app treo vô thời hạn.
- **Retry 2 lần**: lần 1 đợi 2 giây, lần 2 đợi 4 giây (exponential backoff). Xử lý lỗi tạm thời.
- **Circuit Breaker**: sau N lần thất bại liên tiếp, circuit "mở" — ngừng gửi request trong 30 giây, tránh spam server đang down.

**Tầng 2 — Service layer "never throw":** Mọi service method đều bọc trong try-catch, trả về `null` hoặc empty list. Không có exception nào escape ra ViewModel.

**Tầng 3 — UI StateContainer:** Mỗi trang có 4 trạng thái:
- `Loading` → Skeleton placeholders
- `Success` → Hiển thị data
- `Empty` → "Không có dữ liệu"
- `Error` → Thông báo lỗi + nút **Retry**

**Tầng 4 — Offline fallback:** Nếu đã từng tải dữ liệu qua OfflineBundleDownloadService, app dùng SQLite local. Bản đồ Mapsui cũng hoạt động offline sau khi tiles đã cache.

---

### CÂU 4: "Giải thích cách xác thực. Tại sao dùng Cookie mà không dùng JWT?"

**Trả lời:**

Dạ, Web Admin dùng **Cookie Authentication** vì:

1. **Web Admin là MVC server-rendered** — không phải SPA. Cookie là cơ chế tự nhiên nhất cho server-rendered pages. Browser tự gửi cookie mỗi request, không cần viết thêm code JavaScript đính kèm token.

2. **Anti-forgery token (CSRF protection)**: Mọi form POST đều có anti-forgery token. Cookie + anti-forgery là combo chuẩn chống CSRF cho MVC.

3. **Đơn giản và đủ dùng**: Web Admin là hệ thống nội bộ, chỉ nhân viên truy cập. Không cần phức tạp hoá bằng JWT + refresh token.

**Mật khẩu được hash bằng BCrypt** — không lưu plaintext. BCrypt có salt tự động, chống rainbow table attack.

**Cookie hết hạn 7 ngày**, persistent (nhớ sau khi đóng browser).

**Nếu thầy hỏi "Vậy App thì sao?":** App hiện giao tiếp với API qua HTTP không cần authentication cho phần đọc dữ liệu (POI, Tour, Audio là public). Nếu mở rộng, em sẽ thêm JWT cho API — nhưng trong scope đồ án, visitor không cần đăng nhập để tham quan.

---

### CÂU 5: "Database thiết kế ra sao? Giải thích mối quan hệ giữa các bảng."

**Trả lời:**

Dạ, hệ thống có **11 bảng** chính:

**Bảng trung tâm là `pois`** (Points of Interest) — mọi bảng khác đều xoay quanh nó:

- `pois` ← 1:N → `poi_audios` (mỗi POI có nhiều file audio, mỗi file một ngôn ngữ)
- `pois` ← 1:N → `poi_translations` (mỗi POI có nhiều bản dịch)
- `pois` ← 1:N → `poi_media` (hình ảnh, video)
- `pois` ← 1:N → `qr_codes` (mã QR gắn với POI)
- `pois` ← N:M → `tour` (thông qua bảng trung gian `tour_poi` có `OrderIndex`)
- `pois` ← 1:N → `poi_visit_history` (lịch sử tham quan)

**Bảng `users`:**
- `users` ← 1:N → `poi_visit_history` (ai đã xem POI nào)
- `users` ← 1:N → `user_location_log` (GPS tracking)

**Tại sao tách `poi_translations` riêng?** Để hỗ trợ đa ngôn ngữ mà không phải thêm cột vào bảng `pois` mỗi khi có ngôn ngữ mới. Đây là pattern **EAV (Entity-Attribute-Value)** cho i18n — mở rộng ngôn ngữ chỉ cần thêm record, không cần ALTER TABLE.

**Tại sao `tour_poi` có `OrderIndex`?** Vì thứ tự tham quan trong tour là quan trọng — du khách đi từ chuồng hổ → chuồng voi → hồ cá sấu theo trình tự tối ưu. OrderIndex quyết định thứ tự này.

**Precision `decimal(11,8)` cho GPS:** 8 chữ số thập phân ≈ độ chính xác 1.1mm. Đủ để phân biệt 2 chuồng thú cách nhau vài mét.

---

### CÂU 6: "Mở code chức năng QR cho thầy xem. Giải thích logic."

**Trả lời — Flow hoàn chỉnh:**

**Mobile (QrPage.xaml.cs):**
1. Camera quét QR bằng ZXing.Net.Maui
2. Khi detect được barcode → gọi ViewModel
3. ViewModel gọi `ApiService.PostAsync("/api/mobile/qr/lookup", { code })`

**API (MobileController.cs — QrLookup):**
```csharp
// Nhận code dạng: "TCV-1", "TCV-001", "TCV1", "1"
// Bước 1: Parse — loại bỏ prefix "TCV-", "TCV", lấy số
// Bước 2: Tìm trong bảng qr_codes WHERE QrCodeData = code
// Bước 3: Nếu không thấy → tìm theo poi_id trực tiếp
// Bước 4: Trả về POI data hoặc 404
```

**Tại sao support nhiều format QR?**
- `TCV-1`: format chuẩn (admin in ra)
- `TCV-001`: có người thêm số 0
- `TCV1`: thiếu dấu gạch
- `1`: du khách nhập tay qua NumpadPage

Em xử lý tất cả format để tăng tỷ lệ tra cứu thành công (KPI ≥ 90%).

**Fallback:** Nếu camera không hoạt động hoặc QR bị mờ → chuyển sang NumpadPage, du khách nhập số trên bàn phím ảo.

---

### CÂU 7: "Tại sao dùng Mapsui mà không dùng Google Maps?"

**Trả lời:**

1. **Offline**: Mapsui dùng OpenStreetMap tiles — có thể cache và hoạt động offline. Google Maps SDK yêu cầu kết nối internet.
2. **Không cần API key**: Google Maps yêu cầu API key + billing account (có thể bị charge tiền). Mapsui miễn phí hoàn toàn.
3. **Tích hợp .NET native**: Mapsui có package `Mapsui.Maui` tích hợp trực tiếp, không cần bridge qua native platform.
4. **Tuỳ biến cao**: Có thể vẽ boundary polygon, custom pin theo category, vẽ route — tất cả bằng C# code thuần.

**Nhược điểm em thừa nhận**: Mapsui không mượt bằng Google Maps, thiếu Street View và Directions API. Nhưng với bài toán "bản đồ trong khuôn viên Thảo Cầm Viên", Mapsui đủ dùng.

---

### CÂU 8: "Skeleton Loading là gì? Tại sao không dùng spinner?"

**Trả lời:**

**Skeleton Loading** là kỹ thuật hiển thị "bộ xương" của layout (các khối xám placeholder) khi đang chờ dữ liệu, thay vì spinner tròn xoay.

**Tại sao tốt hơn spinner?**
1. **Perceived performance**: Nghiên cứu UX cho thấy skeleton tạo cảm giác app tải nhanh hơn vì user thấy layout đã sẵn sàng.
2. **Giảm layout shift**: Khi data tải xong, nội dung thay thế skeleton đúng vị trí — không bị "nhảy" layout.
3. **Professional**: Các app lớn (Facebook, LinkedIn, YouTube) đều dùng skeleton.

**Em triển khai bằng custom control `SkeletonBlock.xaml`** — có animation shimmer (hiệu ứng sáng chạy qua) để cho thấy app đang hoạt động, không bị đơ.

---

### CÂU 9: "Polly là gì? Exponential backoff nghĩa là gì?"

**Trả lời:**

**Polly** là thư viện .NET chuyên xử lý resilience patterns — giúp app "tự hồi phục" khi gặp lỗi tạm thời.

**Exponential backoff** nghĩa là mỗi lần retry, thời gian chờ tăng gấp đôi:
- Lần 1: chờ 2 giây
- Lần 2: chờ 4 giây
- (Nếu có lần 3: chờ 8 giây)

**Tại sao không retry ngay lập tức?** Vì nếu server đang quá tải, retry ngay chỉ khiến server càng nặng hơn (thundering herd problem). Chờ lâu hơn giữa mỗi lần → cho server thời gian hồi phục.

**Circuit Breaker:** Giống cầu dao điện — khi "mở" (trip), tất cả request đều bị reject ngay mà không gửi đi. Sau 30 giây, circuit chuyển "half-open" — cho 1 request thử. Nếu thành công → đóng lại bình thường. Nếu thất bại → mở tiếp.

```
CLOSED (bình thường) → N lỗi liên tiếp → OPEN (chặn tất cả)
     ↑                                        │
     └── thành công ← HALF-OPEN (thử 1 cái) ←─┘ (sau 30s)
```

---

### CÂU 10: "SemaphoreSlim trong NarrationEngine để làm gì?"

**Trả lời:**

**Vấn đề:** GeofencingEngine chạy trên background thread, khi du khách đi gần 2 POI cùng lúc, có thể trigger 2 narration đồng thời → 2 audio phát cùng lúc → loạn.

**Giải pháp:** `SemaphoreSlim(1, 1)` — cho phép tối đa 1 thread vào critical section.

```csharp
await _semaphore.WaitAsync(); // Nếu đang phát → chờ
try {
    await PlayNarration(poi); // Chỉ 1 audio phát tại một thời điểm
} finally {
    _semaphore.Release(); // Xong → cho thread khác vào
}
```

**Tại sao không dùng `lock`?** Vì `lock` không hỗ trợ `async/await`. Trong context mobile (async everywhere), SemaphoreSlim là lựa chọn đúng đắn.

---

### CÂU 11: "Audio tải về như thế nào? Tại sao dùng MemoryStream?"

**Trả lời:**

**Vấn đề gốc:** Ban đầu em stream audio trực tiếp từ HTTP (NetworkStream). Nhưng khi mạng chập chờn, NetworkStream bị đóng giữa chừng → `ObjectDisposedException` → crash.

**Giải pháp:** Tải toàn bộ audio vào **MemoryStream** trước, rồi mới phát.

```csharp
// TRƯỚC (crash):
var stream = await httpClient.GetStreamAsync(url);
audioPlayer.Play(stream); // Stream bị đóng → crash

// SAU (ổn định):
var bytes = await httpClient.GetByteArrayAsync(url);
var memStream = new MemoryStream(bytes);
audioPlayer.Play(memStream); // Data đã nằm trong RAM → an toàn
```

**Trade-off:** Tốn RAM nhiều hơn (toàn bộ file nằm trong memory). Nhưng file audio thuyết minh thường < 5MB — chấp nhận được trên thiết bị hiện đại.

---

### CÂU 12: "CORS trong API cấu hình sao? Có an toàn không?"

**Trả lời:**

Hiện tại em cấu hình `AllowAnyOrigin, AllowAnyHeader, AllowAnyMethod` — cho phép mọi origin.

**Em thừa nhận đây là cấu hình cho môi trường development.** Trong production, em sẽ:
- Chỉ allow origin từ domain Web Admin cụ thể
- Chỉ allow các method cần thiết (GET, POST, PUT, DELETE)
- Thêm rate limiting

**Tại sao chấp nhận trong scope đồ án?** Vì API chỉ chạy local/intranet, App gọi qua IP nội bộ. Không expose ra public internet.

---

### CÂU 13: "Entity Framework Core hay ADO.NET? Tại sao?"

**Trả lời:**

Em chọn **EF Core (Code-First)** vì:

1. **Productivity**: Viết LINQ thay vì raw SQL — ít lỗi hơn, dễ maintain.
2. **Migrations**: EF Core tự generate migration script khi model thay đổi. Em có 16 migrations, thể hiện quá trình phát triển iterative.
3. **Type safety**: Compile-time check — viết sai tên cột thì IDE báo lỗi ngay.
4. **Cross-database**: Có thể đổi sang PostgreSQL chỉ bằng thay connection string + provider.

**Nếu thầy hỏi "Performance?":** EF Core tạo N+1 query nếu không cẩn thận. Em xử lý bằng `.Include()` (eager loading) và `AsNoTracking()` cho các query chỉ đọc.

---

### CÂU 14: "Cascading Delete có nguy hiểm không?"

**Trả lời:**

Em chỉ áp dụng Cascading Delete cho `PoiTranslations` — khi xóa POI, tất cả bản dịch tự động bị xóa.

**Tại sao?** Bản dịch không có ý nghĩa nếu POI không còn tồn tại. Giữ lại sẽ tạo orphan records — dữ liệu rác.

**Em KHÔNG cascade cho:**
- `poi_audios` → cần xóa file trên filesystem trước rồi mới xóa record
- `tour_poi` → xóa POI không nên xóa luôn tour
- `poi_visit_history` → cần giữ lại cho analytics

**Nguyên tắc:** Cascade chỉ an toàn khi dữ liệu con hoàn toàn phụ thuộc vào cha và không có side effect (file, log).

---

### CÂU 15: "Fallback audio hoạt động thế nào?"

**Trả lời:**

```
Bước 1: User yêu cầu audio tiếng Anh (EN) cho POI "Hổ"
        → API tìm file EN trong bảng poi_audios
        → TÌM THẤY → stream về → PHÁT

Bước 2: Nếu KHÔNG có file EN
        → API fallback về tiếng Việt (VI)
        → TÌM THẤY → stream về → PHÁT

Bước 3: Nếu KHÔNG có file nào
        → App dùng TTS Engine đọc Description của POI
        → TTS hỗ trợ 6 ngôn ngữ: vi, en, th, id, ms, km
        → PHÁT bằng giọng đọc tổng hợp
```

**Tại sao cần fallback 3 tầng?** Vì admin có thể chưa kịp upload audio cho tất cả POI + tất cả ngôn ngữ. Với 23 POI × 3 ngôn ngữ = 69 file audio cần upload. Fallback đảm bảo du khách LUÔN nhận được thông tin.

---

### CÂU 16: "Lược đồ (Database Diagram) em giải thích lại cho thầy."

**Trả lời:**

```
                    ┌──────────────┐
                    │ poi_category │
                    │  CategoryId  │
                    │  CategoryName│
                    └──────┬───────┘
                           │ 1:N
                    ┌──────▼───────┐
                    │     pois     │──── poi_id (PK)
                    │              │──── category_id (FK)
                    │   BẢNG TRUNG │──── name, description
                    │     TÂM      │──── latitude decimal(11,8)
                    │              │──── longitude decimal(11,8)
                    │              │──── radius (default 30)
                    │              │──── priority (1-10)
                    │              │──── image_thumbnail
                    │              │──── is_active, audio_code
                    └──┬──┬──┬──┬──┘
                       │  │  │  │
          ┌────────────┘  │  │  └────────────────┐
          │               │  │                   │
   ┌──────▼──────┐ ┌─────▼──▼─────┐    ┌───────▼──────┐
   │ poi_audios  │ │poi_translations│   │   qr_codes   │
   │  AudioId    │ │TranslationId  │    │    QrId      │
   │  PoiId(FK)  │ │  PoiId (FK)   │   │  PoiId (FK)  │
   │ LanguageCode│ │ LanguageCode  │    │  QrCodeData  │
   │  FileName   │ │  Name         │    │  (TCV-{id})  │
   │  FilePath   │ │  Description  │    └──────────────┘
   │  Duration   │ └───────────────┘
   │  FileSize   │
   └─────────────┘

   ┌──────────┐        ┌──────────────────┐
   │   tour   │──N:M──→│    tour_poi      │
   │  TourId  │        │  TourId (FK)     │
   │  Name    │        │  PoiId (FK)      │
   │EstimTime │        │  OrderIndex ← THỨ TỰ│
   └──────────┘        └──────────────────┘

   ┌──────────┐        ┌──────────────────┐
   │  users   │──1:N──→│poi_visit_history │
   │  UserId  │        │  PoiId (FK)      │
   │  Email   │        │  UserId (FK)     │
   │ Password │        │  VisitTime       │
   │(BCrypt)  │        │  ListenDuration  │
   │  Role    │        └──────────────────┘
   │(0=Admin) │
   │(1=User)  │──1:N──→┌──────────────────┐
   └──────────┘        │user_location_log │
                       │  Lat, Lng        │
                       │  decimal(11,8)   │
                       │  RecordedAt      │
                       └──────────────────┘
```

**Giải thích logic:**

- **Tại sao tách `poi_translations`?** Đa ngôn ngữ. Thêm ngôn ngữ = thêm record, không ALTER TABLE.
- **Tại sao `tour_poi` có `OrderIndex`?** Thứ tự tham quan. Du khách đi POI 1 → POI 5 → POI 3 theo trình tự tối ưu.
- **Tại sao `decimal(11,8)`?** 8 chữ số sau dấu phẩy = chính xác ~1mm. GPS trong khuôn viên cần phân biệt 2 chuồng cách nhau vài mét.
- **Tại sao BCrypt?** Hash + auto-salt. Không thể reverse. Chống rainbow table.
- **Tại sao `poi_audios` tách riêng?** Một POI có nhiều file audio (vi, en, th). Không thể nhét 3 cột audio vào bảng `pois` — sẽ không mở rộng được.

---

### CÂU 17: "Nếu thầy bảo code tại chỗ, em code cái gì?"

**Gợi ý nhanh — 3 thứ em có thể code trong 3 phút:**

**Option A: Thêm một API endpoint mới**
```csharp
// Trong PoisController.cs — thêm endpoint tìm POI theo category
[HttpGet("category/{categoryId}")]
public async Task<IActionResult> GetByCategory(int categoryId)
{
    var pois = await _context.Pois
        .Where(p => p.CategoryId == categoryId && p.IsActive)
        .Include(p => p.PoiAudios)
        .ToListAsync();
    if (!pois.Any()) return NotFound("Không có POI nào trong category này");
    return Ok(pois);
}
```

**Option B: Thêm validation khoảng cách POI**
```csharp
// Validate POI mới không quá gần POI hiện có (< 5m)
var nearbyPoi = await _context.Pois
    .Where(p => p.IsActive)
    .ToListAsync();
foreach (var existing in nearbyPoi)
{
    var distance = Haversine(newPoi.Latitude, newPoi.Longitude,
                             existing.Latitude, existing.Longitude);
    if (distance < 5)
        return BadRequest($"POI quá gần với '{existing.Name}' ({distance:F1}m)");
}
```

**Option C: Thêm một bảng mới + migration**
```bash
# Thêm property vào model → chạy migration
dotnet ef migrations add AddNewFeature
dotnet ef database update
```

---

### CÂU 18: "State machine trong App là gì? Mở code cho thầy xem."

**Trả lời:**

Mỗi trang App có 4 trạng thái được quản lý bởi `StateContainer` control:

```
        ┌─────────┐
        │ LOADING │ ← Vào trang → gọi API
        └────┬────┘
             │
      ┌──────┼──────┐
      ▼      ▼      ▼
 ┌────────┐ ┌─────┐ ┌───────┐
 │SUCCESS │ │EMPTY│ │ ERROR │
 │(có data)│ │(rỗng)│ │(lỗi)  │
 └────────┘ └─────┘ └───┬───┘
                         │
                    [Nhấn Retry]
                         │
                    ┌────▼────┐
                    │ LOADING │ (quay lại)
                    └─────────┘
```

**File code: `Controls/StateContainer.xaml`** — Nhận BindableProperty `State`, hiển thị content tương ứng. ViewModel chỉ cần set `State = "Loading"` hoặc `State = "Error"`.

---

### CÂU 19: "Validate tọa độ POI như thế nào? Boundary là gì?"

**Trả lời:**

**Boundary** là polygon 23 toạ độ xác định ranh giới Thảo Cầm Viên. Khi admin thêm POI mới, hệ thống kiểm tra:

1. **Toạ độ phải nằm trong boundary** — dùng thuật toán Ray Casting: vẽ tia từ điểm ra vô cực, đếm số lần cắt cạnh polygon. Nếu số lần cắt là lẻ → điểm nằm trong polygon.

2. **Khoảng cách tối thiểu 5m với POI khác** — tránh 2 POI chồng lên nhau (dùng Haversine).

**MapController trả về boundary:**
```json
GET /api/map/boundary
→ Mảng 23 toạ độ [lat, lng] tạo thành polygon kín
```

---

### CÂU 20: "Dự án có gì em tự hào nhất? Và có gì chưa hoàn thiện?"

**Trả lời:**

**Tự hào:**
- **Geofencing + NarrationEngine**: Du khách chỉ cần đi bộ, audio tự phát khi đến gần — không cần thao tác gì. Đây là tính năng tạo sự khác biệt.
- **Resilience patterns (Polly)**: App không crash khi mất mạng — đây là điều nhiều app thực tế không làm được.
- **Offline-first**: Tải trước → dùng offline → đây là bài toán thực tế vì Thảo Cầm Viên WiFi yếu.
- **Kiến trúc 4 project**: Shared Models đảm bảo data contract thống nhất.

**Chưa hoàn thiện (em thành thật):**
- CORS chưa restrict cho production
- Chưa có JWT cho API (hiện API public)
- Chưa có unit test tự động
- Analytics dashboard còn cơ bản
- Chưa có rate limiting cho API
- Một số chức năng trong PRD (WUC-14 xuất tour, WUC-16 RBAC chi tiết) chưa triển khai đầy đủ

---

## PHẦN 4: MẸO BẢO VỆ

1. **Khi thầy hỏi "Tại sao?"** → Luôn trả lời theo pattern: "Vì [vấn đề cụ thể], nên em chọn [giải pháp], thay vì [phương án khác] vì [lý do so sánh]."

2. **Khi thầy bảo mở code** → Mở đúng file, giải thích từ trên xuống: "File này là Controller, nhận request, gọi DbContext, trả JSON."

3. **Khi không biết** → "Dạ phần này em chưa triển khai trong scope đồ án, nhưng hướng mở rộng của em là..." Đừng bao giờ nói "em không biết" mà không kèm hướng giải quyết.

4. **Chuẩn bị sẵn 3 file để mở nhanh:**
   - `ApiThaoCamVien/Controllers/MobileController.cs` (logic QR, Home feed, Nearby)
   - `AppThaoCamVien/Services/GeofencingEngine.cs` (Haversine, debounce, cooldown)
   - `AppThaoCamVien/Services/ApiService.cs` (Polly pipeline)

5. **Khi thầy kêu code tại chỗ** → Thêm 1 endpoint GET đơn giản trong Controller có sẵn. Đừng cố viết cái phức tạp.
