# Hướng dẫn Test Automation cho ThaoCamVien (Web + API, nhiều máy)

Tài liệu này bổ sung phần **Set up test automation for multiple machines** cho project. Mục tiêu:

1. Có bộ test tự động cho **Web Admin** (`WebThaoCamVien`) và **API** (`ApiThaoCamVien`).
2. **Nhìn được** test đang chạy như thế nào trên trình duyệt (không phải chạy ngầm).
3. Cùng bộ test đó chạy được trên **nhiều máy** (máy bạn, máy bạn cùng nhóm, CI GitHub Actions) mà không phải sửa code.

Stack đề xuất:

| Lớp | Công cụ | Lý do chọn |
|-----|---------|-----------|
| Web E2E | **Playwright for .NET** | Cùng ngôn ngữ C#, chạy headed/headless/trace, song song nhiều browser, multi-OS |
| API integration | **xUnit + WebApplicationFactory** | Không cần host thật, gọi API in-memory, nhanh |
| Mobile (optional) | **Appium + WinAppDriver/Android** | Để sau, không bắt buộc cho demo |
| Runner | **`dotnet test`** | Một lệnh duy nhất, ai cũng quen |
| Nhiều máy | **Playwright workers + sharding** + GitHub Actions matrix | Chia test thành N phần, mỗi máy chạy 1 phần |

---

## 1. Tạo test project

Tại thư mục gốc solution (chỗ có `ThaoCamVien.slnx`):

```cmd
dotnet new xunit -n ThaoCamVien.Tests -o ThaoCamVien.Tests
dotnet sln ThaoCamVien.slnx add ThaoCamVien.Tests/ThaoCamVien.Tests.csproj
```

Vào `ThaoCamVien.Tests/ThaoCamVien.Tests.csproj`, thêm reference + Playwright:

```cmd
cd ThaoCamVien.Tests
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Playwright.NUnit
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add reference ../WebThaoCamVien/WebThaoCamVien.csproj
dotnet add reference ../ApiThaoCamVien/ApiThaoCamVien.csproj
dotnet build
```

Sau khi build xong, **cài browser cho Playwright** (làm 1 lần / máy):

```cmd
pwsh bin/Debug/net8.0/playwright.ps1 install
```

(Nếu chưa có `pwsh`, cài PowerShell 7 từ <https://github.com/PowerShell/PowerShell>, hoặc dùng `powershell` thay `pwsh` trên Windows 10/11 mới.)

Lệnh trên tải Chromium, Firefox, WebKit về `%USERPROFILE%\AppData\Local\ms-playwright\`.

---

## 2. Cấu hình base URL (chạy được trên nhiều máy)

Đừng hard-code `http://localhost:5180`. Đọc từ biến môi trường:

`ThaoCamVien.Tests/TestConfig.cs`:

```csharp
namespace ThaoCamVien.Tests;

public static class TestConfig
{
    public static string WebBaseUrl =>
        Environment.GetEnvironmentVariable("THAOCAMVIEN_WEB_URL")
        ?? "http://localhost:5180";

    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("THAOCAMVIEN_API_URL")
        ?? "http://localhost:5281";

    public static string AdminUser =>
        Environment.GetEnvironmentVariable("THAOCAMVIEN_ADMIN_USER") ?? "admin";

    public static string AdminPass =>
        Environment.GetEnvironmentVariable("THAOCAMVIEN_ADMIN_PASS") ?? "admin";
}
```

Mỗi máy chỉ cần set biến môi trường khác nhau (hoặc dùng ngrok URL):

```cmd
set THAOCAMVIEN_WEB_URL=https://your-ngrok.ngrok-free.app
dotnet test
```

---

## 3. Test mẫu cho Web Admin

`ThaoCamVien.Tests/Web/AdminDashboardTests.cs`:

```csharp
using Microsoft.Playwright;
using Xunit;

namespace ThaoCamVien.Tests.Web;

public class AdminDashboardTests : IAsyncLifetime
{
    private IPlaywright _pw = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new()
        {
            // Đọc từ env: HEADED=1 dotnet test → mở browser nhìn được
            Headless = Environment.GetEnvironmentVariable("HEADED") != "1",
            SlowMo   = Environment.GetEnvironmentVariable("HEADED") == "1" ? 400 : 0,
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _pw.Dispose();
    }

    [Fact]
    public async Task Admin_login_then_see_active_sessions_widget()
    {
        var ctx  = await _browser.NewContextAsync(new()
        {
            RecordVideoDir = "videos/",                 // 1 video / test
            ViewportSize   = new() { Width = 1280, Height = 800 },
        });
        await ctx.Tracing.StartAsync(new()
        {
            Screenshots = true, Snapshots = true, Sources = true,
        });

        var page = await ctx.NewPageAsync();

        await page.GotoAsync($"{TestConfig.WebBaseUrl}/Admin");

        // Login form (sửa selector cho khớp markup thật)
        await page.FillAsync("input[name=Username]", TestConfig.AdminUser);
        await page.FillAsync("input[name=Password]", TestConfig.AdminPass);
        await page.ClickAsync("button[type=submit]");

        // Sau khi login phải có dashboard với widget "Active App Sessions Now"
        await Assertions.Expect(page.Locator("text=Active App Sessions"))
                        .ToBeVisibleAsync(new() { Timeout = 10_000 });

        await ctx.Tracing.StopAsync(new()
        {
            Path = $"traces/admin-login-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip",
        });
        await ctx.CloseAsync();
    }
}
```

> Chỗ selector `input[name=Username]` / `text=Active App Sessions` cần đối chiếu với HTML thật của trang Admin. Mở trang trong Chrome → F12 → Inspect → copy selector.

---

## 4. Chạy test "thấy được nó đang làm gì"

Đây là phần trả lời thẳng câu hỏi của bạn: **"Làm sao để tôi test trên website mà tôi có thể biết được nó đang hoạt động như nào?"**

Có 4 cách, dùng kết hợp:

### 4.1 Headed mode — mở browser thật, xem từng bước

Trong test ở mục 3, biến `HEADED=1` → browser mở ra, `SlowMo=400ms` mỗi action → mắt người theo dõi kịp.

```cmd
set HEADED=1
dotnet test --filter Admin_login_then_see_active_sessions_widget
```

Bạn sẽ thấy Chrome bật lên, tự nhập user/pass, tự click, tự load dashboard.

### 4.2 Trace Viewer — xem lại sau khi chạy

Trace file `.zip` được Playwright lưu ở `traces/`. Mở bằng:

```cmd
pwsh bin/Debug/net8.0/playwright.ps1 show-trace traces/admin-login-20260512-101530.zip
```

Trace Viewer là một app GUI hiện ra với:

- Timeline mọi action (click, fill, network).
- Screenshot trước/sau mỗi bước.
- DOM snapshot — di chuột lên là thấy element nào được tương tác.
- Network log đầy đủ.
- Console log của browser.

→ Dùng để **debug khi test fail trên CI** (CI chạy headless nên không nhìn được lúc đó).

### 4.3 Video — bằng chứng "test này đã chạy gì"

`RecordVideoDir = "videos/"` ở mục 3 đã bật. Mỗi test fail/pass đều có 1 file `.webm` mở bằng VLC / Chrome xem được. Tốt cho báo cáo đồ án.

### 4.4 Playwright UI mode (interactive)

Đây là "studio" để **viết và chạy thử test** trực quan nhất:

```cmd
pwsh bin/Debug/net8.0/playwright.ps1 codegen http://localhost:5180/Admin
```

→ Bật browser, mỗi thao tác bạn làm tay sẽ được Playwright sinh code C# tương ứng. Dán code đó vào test class là xong.

### 4.5 Khi chỉ muốn test bằng tay (không tự động)

- `WebThaoCamVien` dùng Razor/MVC → cứ `dotnet run` rồi mở `http://localhost:5180/Admin`, F12 mở DevTools tab **Network** để xem từng request gọi đi.
- `ApiThaoCamVien` có Swagger → `https://<host>/swagger`. Mỗi endpoint click "Try it out", điền tham số, xem response. Không cần Postman.

---

## 5. Chạy test trên nhiều máy

### 5.1 Trên 1 máy, song song nhiều luồng (cùng máy nhiều worker)

`ThaoCamVien.Tests/xunit.runner.json`:

```json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4
}
```

→ xUnit chạy 4 test cùng lúc. Playwright tạo 4 browser context tách biệt (cookies, storage tách).

### 5.2 Sharding — chia test ra cho nhiều máy

Mỗi máy chạy 1 phần:

```cmd
:: Máy A
dotnet test --filter "FullyQualifiedName~ThaoCamVien.Tests" -- xunit.execution.AssemblyFileName=A.dll
```

Cách dễ hơn: dùng `dotnet test --filter` chia theo namespace hoặc category:

```cmd
:: Máy 1
dotnet test --filter Category=Smoke

:: Máy 2
dotnet test --filter Category=Regression

:: Máy 3
dotnet test --filter Category=Slow
```

Đánh dấu test bằng:

```csharp
[Fact, Trait("Category", "Smoke")]
public async Task Admin_login_succeeds() { ... }
```

### 5.3 GitHub Actions matrix — tự động chạy trên 3 OS

Tạo `.github/workflows/test.yml`:

```yaml
name: Test
on: [push, pull_request]

jobs:
  test:
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
        shard: [1, 2, 3]
    runs-on: ${{ matrix.os }}
    env:
      THAOCAMVIEN_WEB_URL: http://localhost:5180
      THAOCAMVIEN_API_URL: http://localhost:5281
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }

      - name: Restore + build
        run: dotnet build ThaoCamVien.slnx -c Release

      - name: Install Playwright browsers
        run: pwsh ThaoCamVien.Tests/bin/Release/net8.0/playwright.ps1 install --with-deps

      - name: Start API + Web in background
        shell: pwsh
        run: |
          Start-Process dotnet -ArgumentList 'run --project ApiThaoCamVien --urls http://0.0.0.0:5281'
          Start-Process dotnet -ArgumentList 'run --project WebThaoCamVien --urls http://0.0.0.0:5180'
          Start-Sleep -Seconds 15

      - name: Run shard ${{ matrix.shard }}/3
        run: dotnet test ThaoCamVien.Tests -c Release --filter Shard=${{ matrix.shard }}

      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: trace-${{ matrix.os }}-shard${{ matrix.shard }}
          path: ThaoCamVien.Tests/traces/
```

→ 3 OS × 3 shard = **9 máy ảo chạy song song**. Mỗi máy chỉ chạy 1/3 test → nhanh gấp 3.

Đánh shard bằng trait:

```csharp
[Fact, Trait("Shard", "1")] public async Task Test_A() { ... }
[Fact, Trait("Shard", "2")] public async Task Test_B() { ... }
[Fact, Trait("Shard", "3")] public async Task Test_C() { ... }
```

### 5.4 Test trên thiết bị thật (nhiều browser cùng lúc trên 1 máy)

```csharp
public static IEnumerable<object[]> AllBrowsers => new[]
{
    new object[] { "chromium" },
    new object[] { "firefox" },
    new object[] { "webkit" },
};

[Theory, MemberData(nameof(AllBrowsers))]
public async Task Login_works_on(string browserName)
{
    var browser = browserName switch
    {
        "firefox" => await _pw.Firefox.LaunchAsync(),
        "webkit"  => await _pw.Webkit.LaunchAsync(),
        _         => await _pw.Chromium.LaunchAsync(),
    };
    // ... test code ...
}
```

→ 1 lệnh `dotnet test` chạy 3 browser, bắt được lỗi CSS chỉ xuất hiện trên Safari/Firefox.

---

## 6. Test API (không cần browser)

`ThaoCamVien.Tests/Api/PresenceEndpointTests.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ThaoCamVien.Tests.Api;

public class PresenceEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PresenceEndpointTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Presence_ping_then_admin_sees_one_active_session()
    {
        var sid = Guid.NewGuid().ToString("N");

        var ping = await _client.PostAsJsonAsync("/api/mobile/presence",
            new { sessionId = sid });
        ping.EnsureSuccessStatusCode();

        var active = await _client.GetFromJsonAsync<int>(
            "/Admin/ActiveAppSessionsJson?staleSeconds=90");

        Assert.True(active >= 1, $"Expected >=1 active session, got {active}");
    }
}
```

Cần `ApiThaoCamVien/Program.cs` cuối file có dòng `public partial class Program;` để `WebApplicationFactory<Program>` thấy được class. Nếu file chưa có thì thêm vào.

---

## 7. Lệnh chạy thường ngày

| Tình huống | Lệnh |
|-----------|------|
| Chạy toàn bộ test, headless, nhanh | `dotnet test` |
| Chạy 1 test, mở browser nhìn được | `set HEADED=1 && dotnet test --filter Admin_login_then_see_active_sessions_widget` |
| Chạy smoke test trước khi commit | `dotnet test --filter Category=Smoke` |
| Sinh code test bằng cách click tay | `pwsh ThaoCamVien.Tests/bin/Debug/net8.0/playwright.ps1 codegen http://localhost:5180` |
| Xem lại trace khi CI fail | `pwsh ...\playwright.ps1 show-trace traces\xxx.zip` |
| Test API riêng, không cần browser | `dotnet test --filter FullyQualifiedName~Api` |

---

## 8. Checklist khi onboard máy mới

- [ ] Cài .NET 8 SDK.
- [ ] Clone repo.
- [ ] `dotnet build ThaoCamVien.slnx`.
- [ ] `pwsh ThaoCamVien.Tests/bin/Debug/net8.0/playwright.ps1 install` (lần đầu).
- [ ] Bật `run-demo.bat` (API) + `dotnet run --project WebThaoCamVien` (Web).
- [ ] `dotnet test --filter Category=Smoke` → tất cả pass.
- [ ] Đặt biến env `THAOCAMVIEN_WEB_URL` nếu Web chạy ở host/port khác mặc định.

---

## 9. Bẫy thường gặp

- **Test pass local, fail CI** → 99% là race condition: thêm `await Assertions.Expect(...).ToBeVisibleAsync()` thay cho `Task.Delay`.
- **Playwright không tìm thấy browser** → quên `playwright.ps1 install` trên máy mới.
- **Đăng nhập fail** vì cookie cũ → trong setup gọi `ctx.ClearCookiesAsync()` đầu mỗi test.
- **Test trên ngrok bị chậm** → tăng `Timeout` lên 30s, hoặc test local rồi mới ra ngrok.
- **`PostPresencePingAsync` ném `NotImplementedException`** (đã ghi ở `HUONG_DAN_DEMO.md` mục 3.3) → phải sửa trước, không thì test active sessions sẽ luôn fail.

---

## Tham khảo nhanh

- Playwright .NET docs: <https://playwright.dev/dotnet/>
- `playwright.ps1 codegen` — generator
- `playwright.ps1 show-trace` — debugger
- xUnit traits & filtering: <https://xunit.net/docs/running-tests-in-parallel>
