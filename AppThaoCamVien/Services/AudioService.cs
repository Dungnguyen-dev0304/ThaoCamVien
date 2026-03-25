using Plugin.Maui.Audio;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// Service phát audio thuyết minh.
    /// URL audio được lấy từ bảng PoiMedium (MediaType = "audio").
    /// </summary>
    public class AudioService : IDisposable
    {
        private readonly IAudioManager _audioManager;
        private readonly DatabaseService _databaseService;
        private IAudioPlayer? _currentPlayer;

        private bool _isPlaying = false;
        private int _currentPoiId = -1;
        private long _currentVisitId = -1;
        private DateTime _playStartTime;

        private const string API_BASE_URL = "https://your-api.azurewebsites.net";

        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<double>? ProgressChanged;

        public bool IsPlaying => _isPlaying;
        public double CurrentPosition => _currentPlayer?.CurrentPosition ?? 0;
        public double Duration => _currentPlayer?.Duration ?? 0;

        public AudioService(IAudioManager audioManager, DatabaseService databaseService)
        {
            _audioManager = audioManager;
            _databaseService = databaseService;
        }

        /// <summary>
        /// Phát thuyết minh cho POI. Tự động lấy URL từ PoiMedium.
        /// </summary>
        public async Task PlayPoiAudioAsync(int poiId, int? userId = null)
        {
            try
            {
                // Toggle nếu đang phát đúng bài này
                if (_currentPoiId == poiId && _currentPlayer != null)
                {
                    if (_isPlaying) Pause();
                    else Resume();
                    return;
                }

                await StopAsync();
                _currentPoiId = poiId;

                // Lấy URL audio từ PoiMedium
                var media = await _databaseService.GetAudioForPoiAsync(poiId);
                if (media == null)
                    throw new Exception("Chưa có file audio cho khu vực này.");

                // Xử lý URL: nếu là relative path thì thêm base URL
                var audioUrl = media.MediaUrl.StartsWith("http")
                    ? media.MediaUrl
                    : $"{API_BASE_URL}/{media.MediaUrl.TrimStart('/')}";

                // Log visit
                _currentVisitId = await _databaseService.LogVisitAsync(poiId, userId);
                _playStartTime = DateTime.Now;

                // Stream audio
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var stream = await httpClient.GetStreamAsync(audioUrl);

                _currentPlayer = _audioManager.CreatePlayer(stream);
                _currentPlayer.PlaybackEnded += OnPlaybackEnded;
                _currentPlayer.Play();

                _isPlaying = true;
                PlaybackStateChanged?.Invoke(this, true);
                _ = TrackProgressAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] Error: {ex.Message}");
                throw;
            }
        }

        public void Pause()
        {
            _currentPlayer?.Pause();
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, false);
        }

        public void Resume()
        {
            _currentPlayer?.Play();
            _isPlaying = true;
            PlaybackStateChanged?.Invoke(this, true);
        }

        public async Task StopAsync()
        {
            if (_currentPlayer != null)
            {
                // Lưu thời gian nghe
                if (_currentVisitId > 0)
                {
                    var listenedSeconds = (int)(DateTime.Now - _playStartTime).TotalSeconds;
                    await _databaseService.UpdateListenDurationAsync(_currentVisitId, listenedSeconds);
                }

                _currentPlayer.Stop();
                _currentPlayer.PlaybackEnded -= OnPlaybackEnded;
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }

            _isPlaying = false;
            _currentPoiId = -1;
            _currentVisitId = -1;
        }

        public void SeekTo(double position)
        {
            if (_currentPlayer != null && _currentPlayer.CanSeek)
                _currentPlayer.Seek(position);
        }

        private void OnPlaybackEnded(object? sender, EventArgs e)
        {
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, false);
        }

        private async Task TrackProgressAsync()
        {
            while (_isPlaying && _currentPlayer != null)
            {
                await Task.Delay(500);
                if (_currentPlayer != null && _isPlaying)
                    ProgressChanged?.Invoke(this, _currentPlayer.CurrentPosition);
            }
        }

        public void Dispose()
        {
            _ = StopAsync();
        }
    }
}
