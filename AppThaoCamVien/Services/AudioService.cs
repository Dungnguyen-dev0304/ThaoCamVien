using Plugin.Maui.Audio;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// AudioService — FIX crash stream bị dispose sớm.
    ///
    /// LỖI GỐC: GetStreamAsync() trả về NetworkStream. Khi HttpClient dispose,
    /// stream bị đóng nhưng IAudioPlayer vẫn đọc → crash "Object disposed".
    ///
    /// FIX: GetByteArrayAsync() → MemoryStream(bytes).
    /// MemoryStream không phụ thuộc HttpClient, tự quản lý lifetime.
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly IAudioManager _am;
        private readonly DatabaseService _db;

        private IAudioPlayer? _player;
        private MemoryStream? _stream; // Giữ stream sống trong suốt vòng đời player
        private HttpClient? _http;

        private bool _isPlaying;
        private int _currentPoiId = -1;
        private long _visitId = -1;
        private DateTime _playStart;

        private const string API_BASE = "http://10.0.2.2:5281";

        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<double>? ProgressChanged;

        public bool IsPlaying => _isPlaying;
        public double CurrentPosition => _player?.CurrentPosition ?? 0;
        public double Duration => _player?.Duration ?? 0;

        public AudioService(IAudioManager am, DatabaseService db)
        {
            _am = am;
            _db = db;
        }

        public async Task PlayPoiAudioAsync(int poiId, int? userId = null)
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

                var url = media.MediaUrl.StartsWith("http")
                    ? media.MediaUrl
                    : $"{API_BASE}/{media.MediaUrl.TrimStart('/')}";

                System.Diagnostics.Debug.WriteLine($"[Audio] Download: {url}");

                // Download toàn bộ vào memory — KHÔNG dùng NetworkStream trực tiếp
                _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var bytes = await _http.GetByteArrayAsync(url);
                _stream = new MemoryStream(bytes);
                _stream.Position = 0;

                _player = _am.CreatePlayer(_stream);
                _player.PlaybackEnded += OnPlaybackEnded;
                _player.Play();

                _isPlaying = true;
                _visitId = await _db.LogVisitAsync(poiId, userId);
                _playStart = DateTime.Now;

                PlaybackStateChanged?.Invoke(this, true);
                _ = TrackProgressAsync();

                System.Diagnostics.Debug.WriteLine($"[Audio] Playing {bytes.Length / 1024}KB");
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
                try
                {
                    var secs = (int)(DateTime.Now - _playStart).TotalSeconds;
                    await _db.UpdateListenDurationAsync(_visitId, secs);
                }
                catch { }
                _visitId = -1;
            }

            // Dừng player
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

            // Dispose stream sau player
            try { _stream?.Dispose(); } catch { }
            _stream = null;

            try { _http?.Dispose(); } catch { }
            _http = null;

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
