using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// NarrationEngine — điều phối phát thuyết minh có hàng đợi.
    ///
    /// CƠ CHẾ HÀNG ĐỢI (thêm mới):
    ///   • Khi GPS phát hiện POI mới trong lúc đang phát → không còn bị skip,
    ///     mà được đưa vào queue. Drain loop tự pull POI kế tiếp khi audio
    ///     hiện tại kết thúc.
    ///   • Tối đa 5 POI (1 đang phát + 4 chờ). Vượt quá bỏ POI mới.
    ///   • User tap thủ công (forcePlay=true) → clear queue, ngắt audio, phát
    ///     POI đó ngay.
    ///   • StopAsync → clear queue + stop.
    ///   • SkipAsync → chỉ stop audio hiện tại, drain loop tự advance.
    ///
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

        /// <summary>Tổng số POI tối đa trong queue = 1 đang phát + (MAX_QUEUE-1) chờ.</summary>
        private const int MAX_QUEUE = 5;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _currentPoiId = -1;

        // CTS của lượt phát hiện tại (để forcePlay/skip/stop huỷ download MP3 đang chạy).
        private CancellationTokenSource? _playCts;

        // ── Hàng đợi POI chờ phát ─────────────────────────────────────────
        // _queue KHÔNG chứa POI đang phát. Để biết cái đang phát dùng CurrentPoiId.
        private readonly Queue<Poi> _queue = new();
        private readonly object _queueLock = new();

        /// <summary>
        /// Raised khi queue thay đổi (enqueue / dequeue / clear / current đổi).
        /// UI subscribe để cập nhật panel "Tiếp theo" + nút Skip.
        /// </summary>
        public event EventHandler? QueueChanged;

        // ── Visit tracking ở tầng Narration ──────────────────────────────
        // Đặt ở đây (thay vì AudioService) vì Narration fire CẢ khi phát MP3
        // LẪN khi fallback sang TTS. Trước kia logic nằm trong AudioService
        // nên POI không có MP3 → TTS → không ghi visit. Giờ luôn ghi.
        private long _serverVisitId = -1;
        private DateTime _visitStart;

        // Dedupe prefetch theo "{poiId}_{lang}" để không tải trùng trong 1 session.
        private readonly HashSet<string> _prefetched = new();

        public bool IsPlaying => _audio.IsPlaying || _tts.IsSpeaking;

        /// <summary>POI đang phát hiện tại (-1 nếu không có).</summary>
        public int CurrentPoiId => _currentPoiId;

        private readonly PaymentApiService _paymentApi;

        public NarrationEngine(AudioService audio, DatabaseService db, TtsEngine tts, ApiService api, PaymentApiService paymentApi)
        {
            _audio = audio;
            _db = db;
            _tts = tts;
            _api = api;
            _paymentApi = paymentApi;
        }

        /// <summary>
        /// Premium gate ở tầng NarrationEngine — block AT THE SOURCE.
        /// Mọi caller (Numpad/Qr/Map/Geofencing) đều phải đi qua đây.
        /// Trả về true nếu POI premium VÀ device CHƯA mua → block playback.
        ///
        /// CHÍNH SÁCH FAIL-SAFE: nếu local Poi có IsPremium=true mà API lỗi →
        /// BLOCK (an toàn cho doanh thu Premium hơn là cho phát oan).
        /// </summary>
        private async Task<bool> IsBlockedByPremiumGateAsync(Poi poi)
        {
            // ── 1. Fast path: Poi local biết là Premium ──
            // Nếu Poi đã được sync với cờ IsPremium=true → CHẮC CHẮN premium.
            // Chỉ cần check device access.
            if (poi.IsPremium)
            {
                try
                {
                    var hasAccess = await _paymentApi.HasAccessAsync(poi.PoiId);
                    System.Diagnostics.Debug.WriteLine($"[Narration] Premium POI {poi.PoiId} | hasAccess={hasAccess}");
                    return !hasAccess; // Block nếu chưa mua
                }
                catch (Exception ex)
                {
                    // API lỗi với premium POI → BLOCK (fail-safe nghiêng về bảo vệ doanh thu)
                    System.Diagnostics.Debug.WriteLine($"[Narration] 🔒 Premium check ERROR — BLOCKING by default: {ex.Message}");
                    return true;
                }
            }

            // ── 2. Local nói không premium — vẫn double-check qua API
            // (vì local DB có thể chưa sync IsPremium từ server)
            try
            {
                var info = await _paymentApi.CheckPremiumAsync(poi.PoiId);
                if (info == null)
                {
                    // API không trả lời → POI có thể không có trên server, cho phép phát local
                    return false;
                }
                if (!info.isPremium)
                {
                    return false; // Server xác nhận free → cho phát
                }

                // Server nói Premium nhưng local nói không — server thắng
                var hasAccess = await _paymentApi.HasAccessAsync(poi.PoiId);
                System.Diagnostics.Debug.WriteLine($"[Narration] Server says premium for {poi.PoiId} | hasAccess={hasAccess}");
                return !hasAccess;
            }
            catch (Exception ex)
            {
                // Local nói không premium + API lỗi → cho phát (POI thường không bị khoá oan)
                System.Diagnostics.Debug.WriteLine($"[Narration] Non-premium check failed (allow): {ex.Message}");
                return false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // PUBLIC API — PHÁT / DỪNG / SKIP
        // ═════════════════════════════════════════════════════════════════

        public async Task PlayAsync(Poi poi, bool forcePlay = false)
        {
            if (poi == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] request poi={poi.PoiId} name='{poi.Name}' force={forcePlay}");

                // ═══════════════════════════════════════════════════════════════
                // PREMIUM GATE — BLOCK AT THE SOURCE
                // Mọi caller (Numpad/Qr/Map/Home/Geofencing) đều đi qua đây.
                // Nếu POI premium và device chưa trả tiền → KHÔNG phát.
                // ═══════════════════════════════════════════════════════════════
                if (await IsBlockedByPremiumGateAsync(poi))
                {
                    // Đảm bảo audio đang phát (nếu có) cũng dừng lại
                    try { await _audio.StopAsync(); } catch { }
                    try { await _tts.StopAsync(); } catch { }
                    ClearQueueInternal(raise: true);
                    return;
                }

                // ── TH1: user tap thủ công → ưu tiên cao, clear queue, ngắt current
                if (forcePlay)
                {
                    ClearQueueInternal(raise: false);
                    CancelCurrentPlayback();
                    // Dừng audio/tts để drain loop hiện tại (nếu có) thoát
                    try { await _audio.StopAsync(); } catch { }
                    try { await _tts.StopAsync(); } catch { }

                    if (!await _lock.WaitAsync(5000))
                    {
                        System.Diagnostics.Debug.WriteLine("[Narration] Lock timeout (force)");
                        return;
                    }
                }
                else
                {
                    // ── TH2: GPS auto ──────────────────────────────────────
                    // Nếu đang có drain loop (đang phát) thì luôn enqueue POI mới
                    // (dedupe theo PoiId) để UI có thể hiện "Tiếp theo" + nút Skip.
                    // Không chặn bởi cooldown ở bước này vì user có thể teleport/fake GPS
                    // trong demo; nếu POI đã có trong queue thì EnqueueIfRoom sẽ bỏ qua.
                    if (!_lock.Wait(0))
                    {
                        EnqueueIfRoom(poi);
                        return;
                    }

                    // Không có gì đang phát → áp dụng debounce/cooldown để chống spam GPS.
                    if (IsWithinCooldown(poi))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] debounce/cooldown skip {poi.Name}");
                        _lock.Release();
                        return;
                    }
                }

                // ── Drain loop: pull POI ra khỏi queue cho tới khi rỗng ──
                try
                {
                    var current = poi;
                    while (current != null)
                    {
                        _currentPoiId = current.PoiId;
                        _history[current.PoiId] = DateTime.Now;
                        RaiseQueueChanged();

                        try
                        {
                            await PlayInternalAsync(current, _db.CurrentLanguage);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Narration] drain error: {ex.Message}");
                        }

                        current = TryDequeueOne();
                    }
                }
                finally
                {
                    CancelCurrentPlayback();
                    _currentPoiId = -1;
                    RaiseQueueChanged();
                    _lock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] PlayAsync error: {ex.Message}");
                // KHÔNG rethrow
            }
        }

        /// <summary>Bỏ qua POI đang phát, nhảy sang POI kế tiếp trong queue (nếu có).</summary>
        public async Task SkipAsync()
        {
            System.Diagnostics.Debug.WriteLine("[Narration] ⏭ skip");
            CancelCurrentPlayback();
            try { await _audio.StopAsync(); } catch { }
            try { await _tts.StopAsync(); } catch { }
            // Drain loop đang await _audio.PlaybackStateChanged sẽ kết thúc,
            // rồi TryDequeueOne() sẽ lấy POI kế tiếp. Nếu queue rỗng thì
            // drain loop thoát tự nhiên.
        }

        /// <summary>Dừng phát + xóa toàn bộ queue.</summary>
        public async Task StopAsync()
        {
            try
            {
                ClearQueueInternal(raise: false);
                CancelCurrentPlayback();
                await _audio.StopAsync();
                await _tts.StopAsync();
                RaiseQueueChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] StopAsync error: {ex.Message}");
            }
        }

        private void CancelCurrentPlayback()
        {
            try
            {
                if (_playCts != null && !_playCts.IsCancellationRequested)
                    _playCts.Cancel();
            }
            catch { }
            try { _playCts?.Dispose(); } catch { }
            _playCts = null;
        }

        // ═════════════════════════════════════════════════════════════════
        // QUEUE OPERATIONS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Snapshot các POI đang chờ (không kèm POI đang phát).</summary>
        public IReadOnlyList<Poi> GetQueueSnapshot()
        {
            lock (_queueLock)
            {
                return _queue.ToList();
            }
        }

        /// <summary>Số POI đang chờ trong queue (không tính POI đang phát).</summary>
        public int QueueCount
        {
            get
            {
                lock (_queueLock) return _queue.Count;
            }
        }

        /// <summary>Xóa 1 POI khỏi queue (cho nút X trên panel "Tiếp theo").</summary>
        public void RemoveFromQueue(int poiId)
        {
            bool removed = false;
            lock (_queueLock)
            {
                if (_queue.Any(p => p.PoiId == poiId))
                {
                    var filtered = _queue.Where(p => p.PoiId != poiId).ToList();
                    _queue.Clear();
                    foreach (var p in filtered) _queue.Enqueue(p);
                    removed = true;
                }
            }
            if (removed)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] ✖ removed poiId={poiId} from queue");
                RaiseQueueChanged();
            }
        }

        private void EnqueueIfRoom(Poi poi)
        {
            bool added = false;
            lock (_queueLock)
            {
                if (_currentPoiId == poi.PoiId) return;                         // đang phát chính nó
                if (_queue.Any(p => p.PoiId == poi.PoiId)) return;              // đã có trong queue
                if (_queue.Count >= MAX_QUEUE - 1)                              // trừ 1 cho cái đang phát
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] queue FULL ({_queue.Count}), drop '{poi.Name}'");
                    return;
                }
                _queue.Enqueue(poi);
                added = true;
                System.Diagnostics.Debug.WriteLine($"[Narration] ➕ enqueued '{poi.Name}' (size={_queue.Count})");
            }
            if (added) RaiseQueueChanged();
        }

        private Poi? TryDequeueOne()
        {
            lock (_queueLock)
            {
                return _queue.Count > 0 ? _queue.Dequeue() : null;
            }
        }

        private void ClearQueueInternal(bool raise = true)
        {
            bool had;
            lock (_queueLock)
            {
                had = _queue.Count > 0;
                _queue.Clear();
            }
            if (had && raise) RaiseQueueChanged();
        }

        private void RaiseQueueChanged()
        {
            try { QueueChanged?.Invoke(this, EventArgs.Empty); } catch { }
        }

        private bool IsWithinCooldown(Poi poi)
        {
            if (!_history.TryGetValue(poi.PoiId, out var last)) return false;
            var elapsed = (DateTime.Now - last).TotalSeconds;
            return elapsed < DEBOUNCE_SEC || elapsed < COOLDOWN_MIN * 60;
        }

        // ═════════════════════════════════════════════════════════════════
        // PLAY INTERNAL — logic phát MP3 / TTS fallback
        // ═════════════════════════════════════════════════════════════════

        private async Task PlayInternalAsync(Poi poi, string lang)
        {
            System.Diagnostics.Debug.WriteLine($"[Narration] ▶ '{poi.Name}' [{lang}]");

            // Tạo token cho lượt phát này, để forcePlay/skip có thể cancel ngay cả khi đang download MP3.
            CancelCurrentPlayback();
            _playCts = new CancellationTokenSource();
            var ct = _playCts.Token;

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
                    // ── FIX race: Subscribe handler TRƯỚC khi gọi PlayPoiAudioAsync ──
                    // PlayPoiAudioAsync gọi StopAsync() nội bộ để dọn POI trước → fire
                    // spurious PlaybackStateChanged(false). Sau đó tạo player mới →
                    // fire (true). Nếu subscribe SAU khi PlayPoiAudioAsync return, có
                    // race: (false) spurious có thể tới sau lúc subscribe nếu audio
                    // callback chạy trên thread khác → complete done ngay → POI B
                    // bị cắt ngang chưa kịp phát. Dùng flag sawPlayingTrue để chỉ
                    // chấp nhận (false) SAU khi đã thấy (true).
                    var done = new TaskCompletionSource<bool>();
                    var sawPlayingTrue = false;
                    EventHandler<bool>? h = null;
                    h = (_, playing) =>
                    {
                        if (playing) sawPlayingTrue = true;
                        else if (sawPlayingTrue) done.TrySetResult(true);
                    };
                    _audio.PlaybackStateChanged += h;

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] source=audio-file url={media.MediaUrl}");
                        await _audio.PlayPoiAudioAsync(poi.PoiId, ct: ct);
                        System.Diagnostics.Debug.WriteLine("[Narration] 🎵 MP3 playing");

                        // Chờ MP3 kết thúc (max 15 phút) hoặc bị skip/stop
                        try
                        {
                            await done.Task.WaitAsync(TimeSpan.FromMinutes(15));
                        }
                        catch (TimeoutException) { }
                        return; // MP3 thành công (hoặc bị skip) — drain loop tự pull tiếp
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("[Narration] MP3 cancelled");
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] MP3 failed: {ex.Message} → TTS");
                        try { await _audio.StopAsync(); } catch { }
                    }
                    finally
                    {
                        _audio.PlaybackStateChanged -= h;
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

        // ═════════════════════════════════════════════════════════════════
        // MISC
        // ═════════════════════════════════════════════════════════════════

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
