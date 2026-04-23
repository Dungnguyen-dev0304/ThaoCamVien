using Plugin.Maui.Audio;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// AudioService — PHƯƠNG ÁN TỐI ƯU cho tốc độ load thuyết minh.
    ///
    /// CẢI TIẾN SO VỚI BẢN CŨ:
    ///   1. Disk cache ở {CacheDirectory}/poi_audio/{poiId}_{lang}.ext
    ///      → Lần 2 trở đi phát TỨC THÌ, không tốn mạng.
    ///   2. Một HttpClient static dùng chung (connection pool reuse)
    ///      → Không lặp DNS/TCP/TLS handshake (tiết kiệm 300ms–1.5s mỗi lần).
    ///   3. URL build qua MediaUrlResolver (tôn trọng BaseUrl động do
    ///      App.xaml.cs cấu hình, không còn hardcode 10.0.2.2:5281).
    ///   4. Streaming-to-disk với HttpCompletionOption.ResponseHeadersRead
    ///      → Ghi file ngay khi header về, không chờ đủ body trong RAM.
    ///   5. PrefetchAsync(poiId) — LocationService có thể nạp nền POI lân cận.
    ///
    /// LỖI GỐC đã fix (giữ nguyên): stream đóng sớm khi HttpClient dispose.
    /// Hiện tại stream là FileStream (từ cache), sống độc lập khỏi HttpClient.
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly IAudioManager _am;
        private readonly DatabaseService _db;
        private readonly ApiService _api;

        private IAudioPlayer? _player;
        private FileStream? _stream; // stream từ file cache — sống trong vòng đời player

        // ✅ Một HttpClient duy nhất cho mọi lần download.
        //    Tái sử dụng connection pool: không phải DNS/TCP/TLS lại mỗi lần.
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        private bool _isPlaying;
        private int _currentPoiId = -1;
        private long _visitId = -1;          // ID trong SQLite local
        private long _serverVisitId = -1;    // ID do API trả về (cho PATCH duration)
        private DateTime _playStart;

        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<double>? ProgressChanged;

        public bool IsPlaying => _isPlaying;
        public double CurrentPosition => _player?.CurrentPosition ?? 0;
        public double Duration => _player?.Duration ?? 0;

        public AudioService(IAudioManager am, DatabaseService db, ApiService api)
        {
            _am = am;
            _db = db;
            _api = api;
        }

        // ──────────────────────────────────────────────────────────────
        // CACHE HELPERS
        // ──────────────────────────────────────────────────────────────

        private static string CacheDir
        {
            get
            {
                var dir = Path.Combine(FileSystem.CacheDirectory, "poi_audio");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Suy đoán extension (.mp3/.m4a/...) từ URL để đặt tên cache hợp lý.
        /// Uri.AbsolutePath tự bỏ query string → đuôi file luôn sạch.</summary>
        private static string GuessExtension(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var ext = Path.GetExtension(path);
                return string.IsNullOrWhiteSpace(ext) ? ".mp3" : ext;
            }
            catch { return ".mp3"; }
        }

        /// <summary>
        /// Hash ngắn 8 ký tự của URL. URL thay đổi (vd admin upload file mới,
        /// API nhúng UpdatedAt vào query string) → hash đổi → cache path đổi →
        /// cache miss → tải lại bản mới nhất. Vẫn là "cache" theo POI+lang nhưng
        /// có khả năng tự-invalidate khi server thay đổi file.
        /// </summary>
        private static string ShortHash(string s)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty));
            return Convert.ToHexString(bytes).Substring(0, 8).ToLowerInvariant();
        }

        private static string CachePathFor(int poiId, string lang, string url)
            => Path.Combine(CacheDir, $"{poiId}_{lang}_{ShortHash(url)}{GuessExtension(url)}");

        /// <summary>
        /// Build URL audio tuyệt đối từ chuỗi media trong DB.
        /// Ưu tiên MediaUrlResolver — helper này đã tôn trọng ApiService.BaseUrl
        /// (swap 5281 → 5181) và preference "WebBaseUrl".
        /// </summary>
        private static string ResolveAudioUrl(string mediaUrl)
        {
            if (string.IsNullOrWhiteSpace(mediaUrl)) return string.Empty;
            if (mediaUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return mediaUrl;
            return MediaUrlResolver.AudioUrlFor(mediaUrl);
        }

        /// <summary>
        /// Tải file về thư mục cache với pattern atomic (.tmp → rename).
        /// Dùng ResponseHeadersRead để không phải buffer cả file trong RAM
        /// trước khi ghi đĩa — tiết kiệm bộ nhớ và giảm độ trễ cảm nhận.
        /// </summary>
        private static async Task DownloadToCacheAsync(
            string url, string cachePath, CancellationToken ct)
        {
            var tmp = cachePath + ".tmp";
            try
            {
                using var resp = await _http
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                using (var dst = File.Create(tmp))
                {
                    await src.CopyToAsync(dst, 64 * 1024, ct).ConfigureAwait(false);
                }

                if (File.Exists(cachePath))
                {
                    try { File.Delete(cachePath); } catch { }
                }
                File.Move(tmp, cachePath);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Nạp sẵn audio của POI vào cache, KHÔNG phát. Dùng cho prefetch
        /// POI lân cận (vd LocationService phát hiện POI sắp tới).
        /// Silent fail — mạng hỏng thì thôi, khi user bấm Play thì vẫn
        /// download bình thường.
        /// </summary>
        public async Task PrefetchAsync(int poiId, CancellationToken ct = default)
        {
            try
            {
                var media = await _db.GetAudioForPoiAsync(poiId);
                if (media == null || string.IsNullOrWhiteSpace(media.MediaUrl)) return;

                var url = ResolveAudioUrl(media.MediaUrl);
                if (string.IsNullOrWhiteSpace(url)) return;

                var lang = _db.CurrentLanguage;
                var path = CachePathFor(poiId, lang, url);
                if (File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[Audio] prefetch POI#{poiId} already cached");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[Audio] prefetch POI#{poiId} lang={lang} url={url}");
                await DownloadToCacheAsync(url, path, ct).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[Audio] prefetch POI#{poiId} OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] prefetch POI#{poiId} fail: {ex.Message}");
            }
        }

        /// <summary>Xóa toàn bộ cache audio đã tải. Dùng khi đổi server hoặc cache quá to.</summary>
        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                    Directory.Delete(CacheDir, recursive: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] ClearCache fail: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────
        // PLAY / PAUSE / RESUME / STOP
        // ──────────────────────────────────────────────────────────────

        public async Task PlayPoiAudioAsync(int poiId, int? userId = null, CancellationToken ct = default)
        {
            try
            {
                // Toggle nếu đang phát đúng bài
                if (_currentPoiId == poiId && _player != null)
                {
                    if (_isPlaying) Pause(); else Resume();
                    return;
                }

                await StopAsync();
                _currentPoiId = poiId;

                var media = await _db.GetAudioForPoiAsync(poiId);
                if (media == null)
                    throw new InvalidOperationException($"Không có audio cho POI #{poiId}");

                var url = ResolveAudioUrl(media.MediaUrl);
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException($"URL audio không hợp lệ cho POI #{poiId}");

                var lang = _db.CurrentLanguage;
                var cachePath = CachePathFor(poiId, lang, url);

                var hit = File.Exists(cachePath);
                var ts = DateTime.Now;
                if (!hit)
                {
                    System.Diagnostics.Debug.WriteLine($"[Audio] cache MISS → download {url}");
                    await DownloadToCacheAsync(url, cachePath, ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Audio] cache HIT {cachePath}");
                }
                var tookMs = (DateTime.Now - ts).TotalMilliseconds;

                // Mở FileStream read-only, share-read. Stream sống đến khi StopAsync.
                _stream = File.Open(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _player = _am.CreatePlayer(_stream);
                _player.PlaybackEnded += OnPlaybackEnded;
                ct.ThrowIfCancellationRequested();
                _player.Play();

                _isPlaying = true;
                _visitId = await _db.LogVisitAsync(poiId, userId);
                _playStart = DateTime.Now;

                // NOTE: Visit tracking (StartPoiVisit + PATCH duration) đã chuyển
                // sang NarrationEngine.PlayInternalAsync để đảm bảo LUÔN ghi được
                // visit dù là MP3 path hay TTS fallback. Không fire ở đây nữa để
                // tránh double-count. AudioService chỉ còn giữ _visitId local
                // (SQLite) cho thống kê trong app.

                PlaybackStateChanged?.Invoke(this, true);
                _ = TrackProgressAsync();

                System.Diagnostics.Debug.WriteLine(
                    $"[Audio] Playing POI#{poiId} ({(hit ? "cached" : "downloaded")} in {tookMs:0}ms)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] PlayPoiAudio error: {ex.Message}");
                _currentPoiId = -1;
                throw; // Re-throw để NarrationEngine biết fallback sang TTS
            }
        }

        public void Pause()
        {
            if (!_isPlaying) return;
            try { _player?.Pause(); } catch { }
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, false);
        }

        public void Resume()
        {
            if (_isPlaying) return;
            try { _player?.Play(); } catch { }
            _isPlaying = true;
            PlaybackStateChanged?.Invoke(this, true);
        }

        public async Task StopAsync()
        {
            // Lưu thời gian nghe
            if (_visitId > 0)
            {
                var secs = (int)(DateTime.Now - _playStart).TotalSeconds;
                try
                {
                    await _db.UpdateListenDurationAsync(_visitId, secs);
                }
                catch { }

                // NOTE: PATCH duration lên server đã chuyển sang
                // NarrationEngine.PlayInternalAsync (finally block). Ở đây
                // chỉ cập nhật SQLite local cho màn hình thống kê trong app.

                _visitId = -1;
                _serverVisitId = -1;
            }

            // Dừng player trước, dispose stream sau
            if (_player != null)
            {
                try
                {
                    _player.PlaybackEnded -= OnPlaybackEnded;
                    _player.Stop();
                    _player.Dispose();
                }
                catch { }
                _player = null;
            }

            try { _stream?.Dispose(); } catch { }
            _stream = null;

            _isPlaying = false;
            _currentPoiId = -1;

            try { PlaybackStateChanged?.Invoke(this, false); } catch { }
        }

        public void SeekTo(double position)
        {
            try
            {
                if (_player?.CanSeek == true) _player.Seek(position);
            }
            catch { }
        }

        private void OnPlaybackEnded(object? sender, EventArgs e)
        {
            _isPlaying = false;
            try { PlaybackStateChanged?.Invoke(this, false); } catch { }
        }

        private async Task TrackProgressAsync()
        {
            while (_isPlaying && _player != null)
            {
                await Task.Delay(500);
                try
                {
                    if (_isPlaying && _player != null)
                        ProgressChanged?.Invoke(this, _player.CurrentPosition);
                }
                catch { break; }
            }
        }

        public void Dispose() => _ = StopAsync();
    }
}
