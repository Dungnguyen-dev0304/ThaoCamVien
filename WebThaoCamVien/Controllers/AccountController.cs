using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebThaoCamVien.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        // Bảng Users đã bị bỏ — admin giờ đọc từ appsettings.json:
        //   "Admin": { "Email": "...", "Password": "...", "DisplayName": "..." }
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config)
        {
            _config = config;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Admin");
            }
            return View();
        }

        // POST: /Account/Login
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập Email và Mật khẩu.";
                return View();
            }

            var adminEmail = _config["Admin:Email"];
            var adminPassword = _config["Admin:Password"];
            var adminDisplayName = _config["Admin:DisplayName"] ?? "Quản trị viên";

            if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
            {
                ViewBag.Error = "Chưa cấu hình tài khoản admin trong appsettings.json.";
                return View();
            }

            // So sánh thẳng (demo). Production nên hash password.
            var isMatch = string.Equals(email, adminEmail, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(password, adminPassword, StringComparison.Ordinal);

            if (isMatch)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "admin"),
                    new Claim(ClaimTypes.Email, adminEmail),
                    new Claim(ClaimTypes.Name, adminDisplayName),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToAction("Index", "Admin");
            }

            ViewBag.Error = "Email hoặc mật khẩu không chính xác.";
            return View();
        }

        // GET: /Account/Logout
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
