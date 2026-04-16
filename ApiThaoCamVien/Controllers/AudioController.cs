using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AudioController : ControllerBase
    {
        private readonly WebContext _ctx;
        private readonly ILogger<AudioController> _logger;

        // Base URL để app build đường dẫn stream audio
        // Ví dụ: http://192.168.1.10:5281/audio/pois/ten-file.mp3
        private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

        public AudioController(WebContext ctx, ILogger<AudioController> logger)
        {
            _ctx = ctx;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Audio/{poiId}
        // Trả về toàn bộ audio của 1 POI (tất cả ngôn ngữ)
        // App dùng để hiển thị danh sách ngôn ngữ có thể chọn
        // ─────────────────────────────────────────────────────────
        [HttpGet("{poiId:int}")]
        public async Task<IActionResult> GetByPoi(int poiId)
        {
            try
            {
                var poi = await _ctx.Pois.FindAsync(poiId);
                if (poi == null)
                    return NotFound(new { message = $"POI #{poiId} không tồn tại" });

                var audios = await _ctx.PoiAudios
                    .Where(a => a.PoiId == poiId)
                    .OrderBy(a => a.LanguageCode)
                    .ToListAsync();

                var result = audios.Select(a => new
                {
                    a.AudioId,
                    a.PoiId,
                    a.LanguageCode,
                    a.FileName,
                    StreamUrl = $"{BaseUrl}{a.FilePath}", // URL đầy đủ để app phát
                    FileSizeBytes = a.FileSizeBytes,
                    UpdatedAt = a.UpdatedAt
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetByPoi");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Audio/{poiId}/{lang}
        // Trả về audio theo POI + ngôn ngữ cụ thể
        // App gọi cái này khi user đến gần POI để phát audio
        // Ví dụ: GET /api/Audio/3/vi  hoặc  /api/Audio/3/en
        // Nếu không có ngôn ngữ yêu cầu → fallback về tiếng Việt
        // ─────────────────────────────────────────────────────────
        [HttpGet("{poiId:int}/{lang}")]
        public async Task<IActionResult> GetAudio(int poiId, string lang)
        {
            try
            {
                var poi = await _ctx.Pois.FindAsync(poiId);
                if (poi == null)
                    return NotFound(new { message = $"POI #{poiId} không tồn tại" });

                // Tìm audio đúng ngôn ngữ
                var audio = await _ctx.PoiAudios
                    .FirstOrDefaultAsync(a => a.PoiId == poiId && a.LanguageCode == lang);

                // Fallback về tiếng Việt nếu không có ngôn ngữ yêu cầu
                if (audio == null && lang != "vi")
                {
                    _logger.LogWarning("Audio [{Lang}] not found for POI #{PoiId}, falling back to vi", lang, poiId);
                    audio = await _ctx.PoiAudios
                        .FirstOrDefaultAsync(a => a.PoiId == poiId && a.LanguageCode == "vi");
                }

                if (audio == null)
                    return NotFound(new { message = $"Chưa có audio cho POI #{poiId} ngôn ngữ [{lang}]" });

                return Ok(new
                {
                    audio.AudioId,
                    audio.PoiId,
                    audio.LanguageCode,
                    audio.FileName,
                    StreamUrl = $"{BaseUrl}{audio.FilePath}", // App dùng URL này để phát
                    FileSizeBytes = audio.FileSizeBytes,
                    UpdatedAt = audio.UpdatedAt,
                    IsFallback = audio.LanguageCode != lang // true nếu đang dùng bản fallback
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAudio");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}