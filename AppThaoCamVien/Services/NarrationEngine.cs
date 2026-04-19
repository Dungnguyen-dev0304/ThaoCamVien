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
        private readonly ApiService _api;

        private readonly Dictionary<int, DateTime> _history = new();
        private const int DEBOUNCE_SEC = 60;
        private const int COOLDOWN_MIN = 5;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _currentPoiId = -1;

        // ── Visit tracking ở tầng Narration ──────────────────────────────
        // Đặt ở đây (thay vì AudioService) vì Narration fire CẢ khi phát MP3
        // LẪN khi fallback sang TTS. Trước kia logic nằm trong AudioService
        // nên POI không có MP3 → TTS → không ghi visit. Giờ luôn ghi.
        private long _serverVisitId = -1;
        private DateTime _visitStart;

        // Dedupe prefetch theo "{poiId}_{lang}" để không tải trùng trong 1 session.
        private readonly HashSet<string> _prefetched = new();

        public bool IsPlaying => _audio.IsPlaying || _tts.IsSpeaking;

        public NarrationEngine(AudioService audio, DatabaseService db, TtsEngine tts, ApiService api)
        {
            _audio = audio;
            _db = db;
            _tts = tts;
            _api = api;
        }

        public async Task PlayAsync(Poi poi, bool forcePlay = false)
        {
            if (poi == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] request start poiId={poi.PoiId} name='{poi.Name}' force={forcePlay}");
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

            // ── VISIT TRACKING: fire TRƯỚC mọi logic phát, fire-and-forget.
            // Luôn ghi visit dù MP3 hay TTS, dù tải thành công hay không —
            // user đã BẤM Play = 1 lượt thăm.
            _serverVisitId = -1;
            _visitStart = DateTime.Now;
            int poiIdForVisit = poi.PoiId;
            _ = Task.Run(async () =>
            {
                try
                {
                    var id = await _api.StartPoiVisitAsync(poiIdForVisit, 0);
                    _serverVisitId = id;
                    System.Diagnostics.Debug.WriteLine($"[Narration] server visitId={id} (poi={poiIdForVisit})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] StartPoiVisit fail: {ex.Message}");
                    _serverVisitId = -1;
                }
            });

            try
            {
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
                        System.Diagnostics.Debug.WriteLine($"[Narration] source=audio-file url={media.MediaUrl}");
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
                System.Diagnostics.Debug.WriteLine($"[Narration] source=tts lang={lang}");
                await _tts.SpeakPoiAsync(poi, lang);
            }
            finally
            {
                // PATCH duration lên server — fire-and-forget.
                // Nếu _serverVisitId chưa kịp set (Start chưa hoàn tất) thì bỏ qua,
                // server đã có record visit rồi, chỉ thiếu duration.
                try
                {
                    var id = _serverVisitId;
                    if (id > 0)
                    {
                        var secs = (int)(DateTime.Now - _visitStart).TotalSeconds;
                        _ = _api.UpdatePoiVisitDurationAsync(id, secs);
                        System.Diagnostics.Debug.WriteLine($"[Narration] PATCH duration visitId={id} secs={secs}");
                    }
                }
                catch { }
            }
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

        /// <summary>
        /// Nạp sẵn audio POI vào disk cache — không phát, không ảnh hưởng mutex.
        /// Gọi khi GPS phát hiện user đang tiếp cận POI (vùng approaching) để
        /// khi tới nơi là phát được ngay. Fire-and-forget, silent fail.
        /// Dedupe theo ngôn ngữ hiện tại để không tải trùng trong 1 session.
        /// </summary>
        public void PrefetchAudio(int poiId)
        {
            var key = $"{poiId}_{_db.CurrentLanguage}";
            lock (_prefetched)
            {
                if (!_prefetched.Add(key)) return;
            }
            _ = _audio.PrefetchAsync(poiId);
        }

        /// <summary>Quên dedupe prefetch — dùng khi user đổi ngôn ngữ.</summary>
        public void ClearPrefetchDedupe()
        {
            lock (_prefetched) _prefetched.Clear();
        }

        public void Dispose()
        {
            _ = StopAsync();
            _lock.Dispose();
        }
    }
}
