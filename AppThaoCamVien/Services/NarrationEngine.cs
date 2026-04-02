using Microsoft.Maui.Media;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    public class NarrationEngine
    {
        private readonly AudioService _audioService;
        private readonly DatabaseService _databaseService;

        // Từ điển lưu lịch sử để chống spam (Test Case 5)
        private readonly Dictionary<int, DateTime> _playedHistory = new();
        private const int COOLDOWN_MINUTES = 5;

        // Cờ khóa luồng (Mutex/Semaphore) để tránh phát 2 âm thanh đè nhau
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Token để hủy TTS giữa chừng (Test Case 6)
        private CancellationTokenSource? _ttsCts;

        public string CurrentLanguage { get; set; } = "vi";

        public NarrationEngine(AudioService audioService, DatabaseService databaseService)
        {
            _audioService = audioService;
            _databaseService = databaseService;
        }

        // forcePlay = true dùng cho quét QR (bỏ qua Cooldown)
        public async Task PlayNarrativeAsync(Poi poi, bool forcePlay = false)
        {
            // 1. KIỂM TRA COOLDOWN (Chống spam khi lượn lờ ở 1 khu vực)
            if (!forcePlay && _playedHistory.TryGetValue(poi.PoiId, out var lastPlayed))
            {
                if ((DateTime.Now - lastPlayed).TotalMinutes < COOLDOWN_MINUTES)
                {
                    System.Diagnostics.Debug.WriteLine($"[Narration] Bỏ qua {poi.Name} (Đang trong Cooldown).");
                    return;
                }
            }

            // 2. KHÓA LUỒNG: Đảm bảo không có 2 lệnh Play chạy song song
            if (!await _semaphore.WaitAsync(0))
            {
                System.Diagnostics.Debug.WriteLine("[Narration] Đang có tiến trình phát khác, từ chối lệnh mới.");
                return;
            }

            try
            {
                _playedHistory[poi.PoiId] = DateTime.Now;

                // 3. DỪNG MỌI THỨ ĐANG PHÁT
                await StopCurrentNarrationAsync();

                // 4. KIỂM TRA MP3 TRONG DATABASE
                var media = await _databaseService.GetAudioForPoiAsync(poi.PoiId, CurrentLanguage);
                bool isMp3Success = false;

                // Test Case 1 & 2: Có URL nhưng có thể lỗi mạng hoặc file 404
                if (media != null && !string.IsNullOrWhiteSpace(media.MediaUrl))
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Narration] Thử phát MP3 cho {poi.Name}...");
                        await _audioService.PlayPoiAudioAsync(poi.PoiId);
                        isMp3Success = true; // Phát MP3 thành công!
                    }
                    catch (Exception ex)
                    {
                        // Lỗi stream (rớt mạng, file hỏng) -> Ghi log và cho rớt xuống TTS
                        System.Diagnostics.Debug.WriteLine($"[Narration] MP3 lỗi ({ex.Message}). Fallback sang TTS.");
                        isMp3Success = false;
                    }
                }

                // 5. FALLBACK SANG TTS (Test Case 3)
                if (!isMp3Success)
                {
                    await PlayTTSAsync(poi);
                }
            }
            finally
            {
                // Luôn mở khóa luồng dù có lỗi hay không
                _semaphore.Release();
            }
        }

        // Hàm xử lý Text To Speech với Cancellation Token
        private async Task PlayTTSAsync(Poi poi)
        {
            _ttsCts = new CancellationTokenSource();

            // 1. LẤY BẢN DỊCH TỰ ĐỘNG TỪ GOOGLE
            var translator = IPlatformApplication.Current.Services.GetService<AutoTranslateService>();
            string textToSpeak = poi.Description ?? $"Chào mừng bạn đến với khu vực {poi.Name}.";

            if (translator != null && CurrentLanguage != "vi")
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] Đang nhờ Google dịch sang {CurrentLanguage}...");
                // Dịch nội dung sang ngôn ngữ hiện tại
                textToSpeak = await translator.TranslateAsync(textToSpeak, CurrentLanguage);
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] Đang đọc TTS: {textToSpeak}");

                // 2. CHỌN GIỌNG ĐỌC TƯƠNG ỨNG CỦA ĐIỆN THOẠI
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                Locale locale = locales.FirstOrDefault(l => l.Language.StartsWith(CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("vi")); // Fallback tiếng Việt

                var options = new SpeechOptions() { Pitch = 1.0f, Volume = 1.0f, Locale = locale };

                await TextToSpeech.Default.SpeakAsync(textToSpeak, options, cancelToken: _ttsCts.Token);
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[Narration] TTS bị ngắt ngang.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] Lỗi TTS: {ex.Message}");
            }
        }

        // Hàm cưỡng chế dừng mọi âm thanh đang phát
        public async Task StopCurrentNarrationAsync()
        {
            // Cắt MP3
            if (_audioService.IsPlaying)
            {
                await _audioService.StopAsync();
            }

            // Cắt TTS
            if (_ttsCts != null && !_ttsCts.IsCancellationRequested)
            {
                _ttsCts.Cancel();
                _ttsCts.Dispose();
                _ttsCts = null;
            }
        }
    }
}