using ApiThaoCamVien.Models; // Chứa WebContext
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Configuration;
using SharedThaoCamVien.Models; // Chứa User model
using System.Security.Claims;

namespace WebThaoCamVien.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly WebContext _context;

        public AccountController(WebContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // Nếu đã đăng nhập thì vào thẳng Admin
            if (User.Identity.IsAuthenticated)
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


            var checkEmail = email; // Đặt breakpoint ở đây (bấm F9)
            var allUsers = _context.Users.ToList(); // Ép Entity Framework lôi hết user ra

            // Tìm user trong database (So sánh trực tiếp password)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.Password == password);

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.DisplayName),
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

                // Chuyển hướng về trang Index trong folder Admin
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