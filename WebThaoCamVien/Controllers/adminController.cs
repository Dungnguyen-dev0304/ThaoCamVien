using Microsoft.AspNetCore.Mvc;

namespace WebThaoCamVien.Controllers
{
    [Route("admin")] // Khi gõ localhost/admin sẽ vào đây
    public class adminController : Controller
    {
        [Route("index")] // URL: localhost/admin/index
        public IActionResult index() => View();

        [Route("poi")] // URL: localhost/admin/poi
        public IActionResult poi() => View();

        [Route("audio")] // URL: localhost/admin/audio
        public IActionResult audio() => View();

        [Route("tour")] // URL: localhost/admin/tour
        public IActionResult tour() => View();

        [Route("history")] // URL: localhost/admin/history
        public IActionResult history() => View();

        [Route("login")] // URL: localhost/admin/login
        public IActionResult login() => View();
    }
}