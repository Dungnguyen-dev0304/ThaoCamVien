# Hướng dẫn demo ThaoCamVien trên nhiều máy (không cần cùng WiFi)

Mục tiêu: sau khi cài app vào điện thoại, **rút USB ra, mang đi bất kỳ đâu có 4G/WiFi** vẫn dùng được, và trang web Admin vẫn đếm được số thiết bị đang online.

Có 2 phần:

1. Chạy API local + expose ra Internet bằng **ngrok** (script có sẵn).
2. Nhập URL ngrok vào app lần đầu chạy → app nhớ và dùng mãi.
3. Mở trang Admin để xem số thiết bị đang dùng app.

---

## 1. Chuẩn bị một lần duy nhất

### 1.1 Cài ngrok

- Tải ngrok: <https://ngrok.com/download> (bản Windows, giải nén lấy `ngrok.exe`).
- Đưa `ngrok.exe` vào PATH, hoặc copy vào `C:\Windows\System32\`.
- Tạo tài khoản free tại <https://dashboard.ngrok.com/signup>, lấy authtoken ở mục "Your Authtoken".
- Mở CMD chạy:

```cmd
ngrok config add-authtoken <dán_token_vào_đây>
```

Chỉ cần làm 1 lần / máy.

### 1.2 SQL Server

`appsettings.json` của `ApiThaoCamVien` đang dùng `Server=.;Database=web;Trusted_Connection=True`. Bạn vẫn phải có SQL Server Express / LocalDB với database `web` trên **máy dev đang chạy API**. Điện thoại không gọi trực tiếp SQL — nó gọi API qua ngrok → API gọi SQL local.

---

## 2. Quy trình mỗi lần demo

### 2.1 Bật API + ngrok

Mở Explorer vào thư mục project, double-click:

```
run-demo.bat
```

Script sẽ:

1. Kiểm tra đã cài `dotnet` và `ngrok` chưa.
2. Mở cửa sổ 1 chạy API trên `http://0.0.0.0:5281`.
3. Mở cửa sổ 2 chạy ngrok tunnel.

Sau ~8 giây cửa sổ ngrok hiện dòng kiểu:

```
Forwarding   https://a1b2-14-177-xxx.ngrok-free.app -> http://localhost:5281
```

**Copy đoạn `https://a1b2-14-177-xxx.ngrok-free.app`** — đây là URL public, điện thoại ở bất kỳ mạng nào cũng truy cập được.

> **Lưu ý:** URL này đổi mỗi lần bật lại ngrok (trừ khi mua plan trả phí có custom domain). Nên mỗi buổi demo đều copy lại và nhập vào app. Nếu muốn URL cố định, nâng cấp ngrok (có free reserved domain 1 cái) hoặc chuyển qua **Cloudflare Tunnel** (miễn phí, URL cố định).

### 2.2 Nhập URL vào app

Lần đầu cài app trên điện thoại, màn hình onboarding sẽ có trang **"Cấu hình IP API"** (OnboardingApiConfigPage).

- Vào ô nhập IP, dán **nguyên URL ngrok** (cả `https://...ngrok-free.app`). Không cần thêm `:port`.
- Bấm **Test kết nối** (nếu có nút Test) → hiện xanh là OK.
- Bấm **Tiếp tục**. App lưu vào `Preferences["ApiBaseUrl"]`, rút USB ra vẫn chạy được.

Nếu app đã qua onboarding rồi (đã lưu URL cũ), vào **Settings → Cấu hình API** (hoặc xoá app + cài lại) để nhập URL ngrok mới.

### 2.3 Lặp lại cho từng máy / từng điện thoại

- Máy 1, máy 2, máy 3... chỉ cần cài app từ **1 URL ngrok duy nhất** (không cần mỗi máy có máy tính riêng).
- Mỗi điện thoại đều nhập cùng URL ngrok đó.
- Tất cả điện thoại sẽ cùng gọi về 1 API, cùng ghi vào 1 database — trang web Admin sẽ thấy tất cả.

---

## 3. Theo dõi thiết bị đang dùng app trên Web

Project đã có sẵn:

- Bảng `AppClientPresences` (SessionId + LastSeenUtc).
- `AppPresenceService` trong app MAUI — mỗi 45 giây gửi ping tới `POST /api/mobile/presence`.
- Endpoint `GET /Admin/ActiveAppSessionsJson?staleSeconds=90` trả số device đang "sống".
- Dashboard Admin (`/Admin`) có field `ActiveAppSessionsNow`.

### 3.1 Cách xem

1. Chạy luôn `WebThaoCamVien` song song với API:

    ```cmd
    cd WebThaoCamVien
    dotnet run --urls http://0.0.0.0:5180
    ```

2. Mở trình duyệt: `http://localhost:5180/Admin` → đăng nhập → thấy số thiết bị active.

### 3.2 Nếu muốn xem Admin từ điện thoại / máy khác

Expose thêm Web qua ngrok (chạy 2 tunnel):

```cmd
ngrok http 5180 --log=stdout
```

Hoặc dùng ngrok config file với 2 tunnel cùng lúc (bản free chỉ được 1 tunnel mỗi lần → phải trả phí hoặc chạy 2 tài khoản).

### 3.3 ⚠ Sửa bug đang chặn tính năng presence

Trong `AppThaoCamVien/Services/ApiService.cs` cuối file:

```csharp
internal async Task PostPresencePingAsync(string sid, object value, CancellationToken none)
{
    throw new NotImplementedException();
}
```

Hàm này **đang ném NotImplementedException** → `AppPresenceService` gọi vào là exception → dashboard luôn hiện 0 thiết bị. Thay bằng:

```csharp
internal async Task PostPresencePingAsync(string sessionId, object? extra, CancellationToken ct)
{
    var url = BuildUrl("/api/mobile/presence");
    try
    {
        ApplyLanguageHeader();
        using var resp = await _httpClient.PostAsJsonAsync(
            url,
            new { sessionId },
            ct);
        // Không cần check body — admin chỉ quan tâm LastSeenUtc đã update.
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[ApiService] presence ping fail: {ex.Message}");
    }
}
```

Sau khi sửa, rebuild APK → cài lại → dashboard sẽ đếm đúng.

---

## 4. Luồng tóm tắt cho buổi bảo vệ

```
[Điện thoại 1] ─┐
[Điện thoại 2] ─┼─► 4G / WiFi nhà ─► Internet ─► ngrok ─► localhost:5281 ─► API ─► SQL Server
[Điện thoại N] ─┘                                                            │
                                                                             ▼
                                             [Web Admin] ◄──── ActiveAppSessionsJson
```

Chỉ cần máy dev (máy bạn) bật `run-demo.bat` và còn mạng là cả hệ thống chạy. Điện thoại đi đâu cũng được.

---

## 5. Khi không còn dùng ngrok nữa (deploy thật)

Khi xong đồ án, chuyển API lên VPS/cloud (Azure App Service, Render, Fly.io, v.v.):

1. Deploy `ApiThaoCamVien` → có URL kiểu `https://api.thaocamvien.vn`.
2. Deploy SQL lên Azure SQL / MySQL cloud hoặc cài trên VPS.
3. Sửa `ApiService.ResolveDefaultApiUrl()` trả về URL production.
4. Rebuild APK → user không cần nhập gì, dùng luôn.

---

## 6. Test automation trên nhiều máy

File này chỉ nói chuyện chạy **thật** trên nhiều máy (ngrok + điện thoại). Phần **kiểm thử tự động** (Playwright cho Web Admin, xUnit cho API, sharding cho nhiều máy, GitHub Actions matrix, cách "nhìn được" test đang chạy) đã tách riêng ở [HUONG_DAN_TEST_AUTOMATION.md](HUONG_DAN_TEST_AUTOMATION.md). Đọc file đó nếu muốn:

- Viết test E2E cho trang Admin và **xem browser thật mở ra từng bước** (`HEADED=1`).
- Xem lại Trace Viewer / video khi test fail trên CI.
- Chia bộ test cho nhiều máy chạy song song (sharding + matrix).
- Sinh code test bằng cách click tay (`playwright codegen`).

---

## Checklist trước khi demo

- [ ] SQL Server đang chạy, DB `web` có dữ liệu.
- [ ] `ngrok config add-authtoken ...` đã chạy.
- [ ] Chạy `run-demo.bat` → cả 2 cửa sổ đều lên.
- [ ] Copy URL `https://...ngrok-free.app`.
- [ ] Mở ngrok URL trên trình duyệt, vào `https://<ngrok>/swagger` → thấy Swagger = API sống.
- [ ] Điện thoại test: nhập URL vào app, mở home, thấy dữ liệu POI.
- [ ] Mở `http://localhost:5180/Admin` → `Active App Sessions Now` > 0.
- [ ] (Đã sửa bug `PostPresencePingAsync`.)

Chúc demo suôn sẻ.
