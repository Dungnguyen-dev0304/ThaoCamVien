using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// NarrationEngine — điều phối phát thuyết minh.
    /// FIX: Xử lý tất cả exception, không bao giờ crash app.
    /// </summary>
    public class NarrationEngine : IDisposable
    {
        private readonly AudioService _audio;
        private readonly DatabaseService _db;
        private readonly TtsEngine _tts;

        private readonly Dictionary<int, DateTime> _history = new();
        private const int DEBOUNCE_SEC = 60;
        private const int COOLDOWN_MIN = 5;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _currentPoiId = -1;

        public bool IsPlaying => _audio.IsPlaying || _tts.IsSpeaking;

        public NarrationEngine(AudioService audio, DatabaseService db, TtsEngine tts)
        {
            _audio = audio;
            _db = db;
            _tts = tts;
        }

        public async Task PlayAsync(Poi poi, bool forcePlay = false)
        {
            if (poi == null) return;

            try
            {
                // Đồng bộ ngôn ngữ
                var lang = _db.CurrentLanguage;

                // Cooldown check
                if (!forcePlay && _history.TryGetValue(poi.PoiId, out var last))
                {
                    var elapsed = (DateTime.Now - last).TotalSeconds;
                    if (elapsed < DEBOUNCE_SEC)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] Debounce {poi.Name}");
                        return;
                    }
                    if (elapsed < COOLDOWN_MIN * 60)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] Cooldown {poi.Name}");
                        return;
                    }
                }

                // Mutex
                if (forcePlay)
                {
                    await StopAsync();
                    if (!await _lock.WaitAsync(3000))
                    {
                        System.Diagnostics.Debug.WriteLine("[Narration] Lock timeout");
                        return;
                    }
                }
                else
                {
                    if (!_lock.Wait(0))
                    {
                        System.Diagnostics.Debug.WriteLine("[Narration] Busy, skip GPS trigger");
                        return;
                    }
                }

                try
                {
                    _currentPoiId = poi.PoiId;
                    _history[poi.PoiId] = DateTime.Now;
                    await PlayInternalAsync(poi, lang);
                }
                finally
                {
                    _currentPoiId = -1;
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] PlayAsync error: {ex.Message}");
                // KHÔNG rethrow
            }
        }

        private async Task PlayInternalAsync(Poi poi, string lang)
        {
            System.Diagnostics.Debug.WriteLine($"[Narration] ▶ '{poi.Name}' [{lang}]");

            // Bước 1: Thử phát MP3 từ server
            PoiMedium? media = null;
            try
            {
                media = await _db.GetAudioForPoiAsync(poi.PoiId, lang);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] GetAudio error: {ex.Message}");
            }

            if (media != null && !string.IsNullOrWhiteSpace(media.MediaUrl))
            {
                try
                {
                    await _audio.PlayPoiAudioAsync(poi.PoiId);
                    System.Diagnostics.Debug.WriteLine("[Narration] 🎵 MP3 playing");

                    // Chờ MP3 kết thúc (max 15 phút)
                    var done = new TaskCompletionSource<bool>();
                    EventHandler<bool>? h = null;
                    h = (_, playing) =>
                    {
                        if (!playing) done.TrySetResult(true);
                    };
                    _audio.PlaybackStateChanged += h;
                    try
                    {
                        await done.Task.WaitAsync(TimeSpan.FromMinutes(15));
                    }
                    catch (TimeoutException) { }
                    finally
                    {
                        _audio.PlaybackStateChanged -= h;
                    }
                    return; // MP3 thành công
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] MP3 failed: {ex.Message} → TTS");
                    try { await _audio.StopAsync(); } catch { }
                }
            }

            // Bước 2: TTS fallback
            System.Diagnostics.Debug.WriteLine($"[Narration] 📝 TTS [{lang}]");
            await _tts.SpeakPoiAsync(poi, lang);
        }

        public async Task StopAsync()
        {
            try
            {
                await _audio.StopAsync();
                await _tts.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] StopAsync error: {ex.Message}");
            }
        }

        public void ResetCooldown(int poiId) => _history.Remove(poiId);

        public void Dispose()
        {
            _ = StopAsync();
            _lock.Dispose();
        }
    }
}
