#if ANDROID
using Android.Speech.Tts;
#endif
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// NarrationEngine — điều phối toàn bộ việc phát thuyết minh.
    /// Luồng: cooldown check → mutex lock → thử MP3 → fallback TTS đa ngôn ngữ.
    /// </summary>
    public class NarrationEngine : IDisposable
    {
        private readonly AudioService _audioService;
        private readonly DatabaseService _databaseService;
        private readonly TtsEngine _ttsEngine;

        private readonly Dictionary<int, DateTime> _playedHistory = new();
        private const int COOLDOWN_MINUTES = 5;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public string CurrentLanguage { get; set; } = "vi";

        public NarrationEngine(AudioService audioService, DatabaseService databaseService, TtsEngine ttsEngine)
        {
            _audioService = audioService;
            _databaseService = databaseService;
            _ttsEngine = ttsEngine;
            CurrentLanguage = databaseService.CurrentLanguage;
        }

        public async Task PlayNarrativeAsync(Poi poi, bool forcePlay = false)
        {
            // Đồng bộ ngôn ngữ trước mỗi lần phát
            CurrentLanguage = _databaseService.CurrentLanguage;

            // Cooldown check — bỏ qua nếu forcePlay (user nhấn tay)
            if (!forcePlay && _playedHistory.TryGetValue(poi.PoiId, out var lastPlayed))
            {
                if ((DateTime.Now - lastPlayed).TotalMinutes < COOLDOWN_MINUTES)
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] ⏸ Cooldown: {poi.Name}");
                    return;
                }
            }

            if (forcePlay)
            {
                // Dừng bài cũ ngay lập tức, rồi chờ mutex
                await StopCurrentNarrationAsync();
                await _semaphore.WaitAsync();
            }
            else
            {
                // GPS trigger: nếu đang bận thì bỏ qua (không xếp hàng)
                if (!await _semaphore.WaitAsync(0))
                {
                    System.Diagnostics.Debug.WriteLine("[Narration] 🔒 Đang phát, bỏ qua GPS trigger");
                    return;
                }
            }

            try
            {
                _playedHistory[poi.PoiId] = DateTime.Now;
                await PlayInternalAsync(poi);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task PlayInternalAsync(Poi poi)
        {
            System.Diagnostics.Debug.WriteLine($"[Narration] ▶ '{poi.Name}' [{CurrentLanguage}]");

            // Bước 1: Thử file MP3 từ server
            var media = await _databaseService.GetAudioForPoiAsync(poi.PoiId, CurrentLanguage);
            if (media != null && !string.IsNullOrWhiteSpace(media.MediaUrl))
            {
                try
                {
                    await _audioService.PlayPoiAudioAsync(poi.PoiId);
                    System.Diagnostics.Debug.WriteLine($"[Narration] 🎵 MP3 OK");
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] ⚠️ MP3 lỗi: {ex.Message}");
                }
            }

            // Bước 2: Fallback TTS chất lượng cao
            System.Diagnostics.Debug.WriteLine($"[Narration] 📝 TTS [{CurrentLanguage}]");
            await _ttsEngine.SpeakPoiAsync(poi, CurrentLanguage);
        }

        public async Task StopCurrentNarrationAsync()
        {
            if (_audioService.IsPlaying)
                await _audioService.StopAsync();
            await _ttsEngine.StopAsync();
        }

        public void Dispose()
        {
            _ = StopCurrentNarrationAsync();
            _semaphore.Dispose();
        }
    }
}
