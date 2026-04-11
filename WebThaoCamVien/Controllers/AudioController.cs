using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using ApiThaoCamVien.Models;

namespace WebThaoCamVien.Controllers
{
    [Authorize]
    public class AudioController : Controller
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _env;

        // Các ngôn ngữ hỗ trợ
        private static readonly string[] SupportedLangs = { "vi", "en", "fr", "zh", "ja", "ko" };

        // Thư mục lưu file audio (wwwroot/audio/pois/)
        private string AudioDir => Path.Combine(_env.WebRootPath, "audio", "pois");

        public AudioController(WebContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private void SetViewData(string title)
        {
            ViewData["Active"] = "audio";
            ViewData["Title"] = title;
            ViewData["PageTitle"] = title;
        }

        // ─────────────────────────────────────────────
        // GET: /Audio/Index — Danh sách POI + trạng thái audio
        // ─────────────────────────────────────────────
        public async Task<IActionResult> AudioList()
        {
            SetViewData("Quản lý Audio");

            var pois = await _context.Pois
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var audios = await _context.PoiAudios
                .ToListAsync();

            ViewBag.AudioMap = audios
                .GroupBy(a => a.PoiId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(a => a.LanguageCode, a => a)
                );

            ViewBag.SupportedLangs = SupportedLangs;

            return View(pois);
        }

        // ─────────────────────────────────────────────
        // GET: /Audio/Manage/{poiId} — Quản lý audio của 1 POI
        // ─────────────────────────────────────────────
        [Route("Audio/Manage/{poiId}")]
        public async Task<IActionResult> Manage(int poiId)
        {
            SetViewData("Chi tiết Audio");

            var poi = await _context.Pois.FindAsync(poiId);
            if (poi == null) return NotFound();

            var audios = await _context.PoiAudios
                .Where(a => a.PoiId == poiId)
                .ToListAsync();

            ViewBag.Poi = poi;
            ViewBag.AudioMap = audios.ToDictionary(a => a.LanguageCode, a => a);
            ViewBag.SupportedLangs = SupportedLangs;

            return View(audios);
        }

        // ─────────────────────────────────────────────
        // POST: /Audio/Upload — Upload file audio
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Upload(int PoiId, string LanguageCode, IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file audio.";
                return RedirectToAction("Manage", new { poiId = PoiId });
            }

            var ext = Path.GetExtension(audioFile.FileName).ToLower();
            if (ext != ".mp3" && ext != ".wav" && ext != ".ogg" && ext != ".m4a")
            {
                TempData["Error"] = "Chỉ chấp nhận file .mp3, .wav, .ogg, .m4a";
                return RedirectToAction("Manage", new { poiId = PoiId });
            }

            Directory.CreateDirectory(AudioDir);

            // Lấy tên file gốc từ máy người dùng, sanitize ký tự đặc biệt
            var originalName = Path.GetFileNameWithoutExtension(audioFile.FileName);
            var safeName = System.Text.RegularExpressions.Regex
                .Replace(originalName, @"[^a-zA-Z0-9\-_\.]", "-")
                .Trim('-');
            var fileName = $"{safeName}{ext}";
            var filePath = Path.Combine(AudioDir, fileName);

            // Xóa file cũ nếu tồn tại
            var existing = await _context.PoiAudios
                .FirstOrDefaultAsync(a => a.PoiId == PoiId && a.LanguageCode == LanguageCode);

            if (existing != null && !string.IsNullOrEmpty(existing.FileName))
            {
                var oldPath = Path.Combine(AudioDir, existing.FileName);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            // Lưu file mới
            await using (var stream = new FileStream(filePath, FileMode.Create))
                await audioFile.CopyToAsync(stream);

            if (existing != null)
            {
                existing.FileName = fileName;
                existing.FilePath = $"/audio/pois/{fileName}";
                existing.FileSizeBytes = audioFile.Length;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PoiAudios.Add(new PoiAudio
                {
                    PoiId = PoiId,
                    LanguageCode = LanguageCode,
                    FileName = fileName,
                    FilePath = $"/audio/pois/{fileName}",
                    FileSizeBytes = audioFile.Length,
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tải lên audio [{LanguageCode.ToUpper()}] thành công!";
            return RedirectToAction("Manage", new { poiId = PoiId });
        }

        // ─────────────────────────────────────────────
        // POST: /Audio/SaveRecording — Lưu audio từ record trên web
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> SaveRecording(int PoiId, string LanguageCode, IFormFile recordedBlob)
        {
            if (recordedBlob == null || recordedBlob.Length == 0)
                return Json(new { success = false, message = "Không nhận được dữ liệu ghi âm." });

            Directory.CreateDirectory(AudioDir);

            // Lấy tên file từ tên blob client gửi lên (đã được sanitize ở JS)
            // Fallback về tên mặc định nếu không có
            var clientName = Path.GetFileNameWithoutExtension(recordedBlob.FileName ?? "");
            var safeName = System.Text.RegularExpressions.Regex
                .Replace(clientName, @"[^a-zA-Z0-9\-_]", "-").Trim('-');
            var fileName = string.IsNullOrEmpty(safeName)
                ? $"poi_{PoiId}_{LanguageCode}_rec_{DateTime.Now:yyyyMMddHHmmss}.webm"
                : $"{safeName}.webm";
            var filePath = Path.Combine(AudioDir, fileName);

            // Xóa file cũ
            var existing = await _context.PoiAudios
                .FirstOrDefaultAsync(a => a.PoiId == PoiId && a.LanguageCode == LanguageCode);

            if (existing != null && !string.IsNullOrEmpty(existing.FileName))
            {
                var oldPath = Path.Combine(AudioDir, existing.FileName);
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            await using (var stream = new FileStream(filePath, FileMode.Create))
                await recordedBlob.CopyToAsync(stream);

            if (existing != null)
            {
                existing.FileName = fileName;
                existing.FilePath = $"/audio/pois/{fileName}";
                existing.FileSizeBytes = recordedBlob.Length;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PoiAudios.Add(new PoiAudio
                {
                    PoiId = PoiId,
                    LanguageCode = LanguageCode,
                    FileName = fileName,
                    FilePath = $"/audio/pois/{fileName}",
                    FileSizeBytes = recordedBlob.Length,
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã lưu ghi âm [{LanguageCode.ToUpper()}]!" });
        }

        // ─────────────────────────────────────────────
        // POST: /Audio/Delete — Xóa audio
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Delete(int AudioId, int PoiId)
        {
            var audio = await _context.PoiAudios.FindAsync(AudioId);
            if (audio != null)
            {
                if (!string.IsNullOrEmpty(audio.FileName))
                {
                    var filePath = Path.Combine(AudioDir, audio.FileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                _context.PoiAudios.Remove(audio);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa audio.";
            }
            return RedirectToAction("Manage", new { poiId = PoiId });
        }
    }
}