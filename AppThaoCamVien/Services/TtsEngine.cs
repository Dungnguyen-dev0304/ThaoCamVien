using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// TTS Engine chất lượng cao cho 6 ngôn ngữ.
    /// 
    /// Kỹ thuật làm giọng đọc tự nhiên hơn:
    /// 1. Chia văn bản thành các câu ngắn → mỗi câu phát riêng (nghe tự nhiên hơn 1 đoạn dài)
    /// 2. Chèn khoảng dừng (pause) giữa các câu bằng cách delay
    /// 3. Điều chỉnh Pitch và Rate phù hợp từng ngôn ngữ (tiếng Thái cần pitch cao hơn)
    /// 4. Script thuyết minh có cấu trúc: chào mừng → nội dung → kết
    /// 5. Fallback locale: nếu thiết bị không có giọng đúng ngôn ngữ → dùng giọng EN
    /// </summary>
    public class TtsEngine
    {
        private CancellationTokenSource? _cts;
        private bool _isSpeaking = false;

        public bool IsSpeaking => _isSpeaking;

        // Mapping từ language code → các prefix locale hệ thống
        // Thứ tự là thứ tự ưu tiên: tìm giọng khớp prefix đầu trước, rồi mới fallback
        private static readonly Dictionary<string, string[]> LocalePrefixes = new()
        {
            ["vi"] = new[] { "vi" },
            ["en"] = new[] { "en-US", "en-GB", "en" },
            ["th"] = new[] { "th" },
            ["id"] = new[] { "id" },
            ["ms"] = new[] { "ms" },
            ["km"] = new[] { "km" },   // Khmer — nhiều thiết bị không có, sẽ fallback EN
        };

        // Tốc độ nói tối ưu cho từng ngôn ngữ (pitch và volume)
        // Tiếng Việt: pitch trung bình, tốc độ vừa phải
        // Tiếng Thái: thường nghe tốt hơn ở pitch hơi cao
        private static readonly Dictionary<string, (float pitch, float volume)> VoiceSettings = new()
        {
            ["vi"] = (1.0f, 1.0f),
            ["en"] = (1.0f, 1.0f),
            ["th"] = (1.05f, 1.0f),
            ["id"] = (1.0f, 1.0f),
            ["ms"] = (1.0f, 1.0f),
            ["km"] = (1.0f, 1.0f),
        };

        // =====================================================================
        // HÀM PHÁT CHÍNH
        // =====================================================================
        /// <summary>
        /// Phát thuyết minh cho một POI.
        /// Tự động xây dựng script từ Name + Description của POI.
        /// </summary>
        public async Task SpeakPoiAsync(Poi poi, string languageCode)
        {
            await StopAsync(); // Dừng bài cũ nếu đang phát

            _cts = new CancellationTokenSource();
            _isSpeaking = true;

            try
            {
                // 1. Tìm locale phù hợp trên thiết bị
                var locale = await FindBestLocaleAsync(languageCode);

                // 2. Lấy cài đặt giọng đọc
                var (pitch, volume) = VoiceSettings.GetValueOrDefault(languageCode, (1.0f, 1.0f));
                var options = new SpeechOptions { Locale = locale, Pitch = pitch, Volume = volume };

                // 3. Xây dựng script thuyết minh tự nhiên
                var sentences = BuildNarrationSentences(poi, languageCode);

                System.Diagnostics.Debug.WriteLine(
                    $"[TTS] Phát {sentences.Count} câu | Ngôn ngữ: {languageCode} | Locale: {locale?.Language ?? "system default"}");

                // 4. Phát từng câu với khoảng dừng nhỏ giữa các câu
                foreach (var sentence in sentences)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    await TextToSpeech.Default.SpeakAsync(sentence, options, _cts.Token);

                    // Dừng 300ms giữa các câu — làm giọng đọc nghe tự nhiên, có nhịp điệu
                    if (!_cts.Token.IsCancellationRequested)
                        await Task.Delay(300, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Đã dừng theo yêu cầu");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Lỗi: {ex.Message}");
            }
            finally
            {
                _isSpeaking = false;
            }
        }

        /// <summary>
        /// Dừng TTS đang phát.
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            _isSpeaking = false;

            // Gọi CancelAll để đảm bảo hệ thống TTS dừng hoàn toàn
            try {
                CancellationTokenSource cts = new();

                await TextToSpeech.SpeakAsync("Hello", cancelToken: cts.Token);

                // cancel
                cts.Cancel();
            } catch { }
            await Task.Delay(100); // Cho hệ thống xử lý xong
        }

        // =====================================================================
        // XÂY DỰNG SCRIPT THUYẾT MINH CHẤT LƯỢNG CAO
        // =====================================================================
        /// <summary>
        /// Tách văn bản thành các câu ngắn, có cấu trúc: chào mừng → nội dung → lời kết.
        /// Mỗi câu ngắn → giọng đọc nghe tự nhiên, không bị ngắt quãng giữa chừng.
        /// </summary>
        private List<string> BuildNarrationSentences(Poi poi, string lang)
        {
            var sentences = new List<string>();

            // --- Câu chào mừng (theo ngôn ngữ) ---
            sentences.Add(GetWelcomePhrase(poi.Name, lang));

            // --- Nội dung chính: tách theo dấu câu ---
            if (!string.IsNullOrWhiteSpace(poi.Description))
            {
                var contentSentences = SplitIntoSentences(poi.Description);
                sentences.AddRange(contentSentences);
            }

            // --- Lời kết (theo ngôn ngữ) ---
            sentences.Add(GetClosingPhrase(lang));

            // Loại bỏ câu rỗng
            return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private string GetWelcomePhrase(string poiName, string lang) => lang switch
        {
            "en" => $"Welcome! You are now visiting {poiName}.",
            "th" => $"ยินดีต้อนรับ! คุณกำลังอยู่ที่ {poiName}",
            "id" => $"Selamat datang! Anda sedang berada di {poiName}.",
            "ms" => $"Selamat datang! Anda kini berada di {poiName}.",
            "km" => $"សូមស្វាគមន៍! អ្នកកំពុងស្ថិតនៅ {poiName}",
            _ => $"Chào mừng bạn đến với {poiName}!",  // mặc định tiếng Việt
        };

        private string GetClosingPhrase(string lang) => lang switch
        {
            "en" => "Thank you for visiting Saigon Zoo and Botanical Gardens. Enjoy your exploration!",
            "th" => "ขอบคุณที่มาเยือนสวนสัตว์สีหงส์ ขอให้สนุกกับการสำรวจ!",
            "id" => "Terima kasih telah mengunjungi Kebun Binatang Saigon. Selamat menikmati perjalanan Anda!",
            "ms" => "Terima kasih kerana melawat Zoo Saigon. Selamat menikmati lawatan anda!",
            "km" => "សូមអរគុណដែលបានមកលេងសួនសត្វសៃហ្គន សូមរីករាយជាមួយការរុករករបស់អ្នក!",
            _ => "Cảm ơn bạn đã tham quan Thảo Cầm Viên Sài Gòn. Chúc bạn có một ngày vui vẻ!",
        };

        /// <summary>
        /// Tách đoạn văn thành câu ngắn theo dấu chấm, chấm than, chấm hỏi, dấu phẩy dài.
        /// Các câu quá ngắn (< 10 ký tự) sẽ được ghép với câu sau để tránh nghe rời rạc.
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            // Tách theo các dấu câu phổ biến
            var raw = text
                .Split(new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" },
                       StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            var result = new List<string>();
            var buffer = "";

            foreach (var part in raw)
            {
                // Thêm dấu chấm nếu câu chưa kết thúc bằng dấu câu
                var sentence = part.TrimEnd();
                if (!sentence.EndsWith('.') && !sentence.EndsWith('!') && !sentence.EndsWith('?'))
                    sentence += ".";

                // Ghép câu ngắn với câu trước để tránh nghe ngắt quãng
                if (buffer.Length > 0 && part.Length < 15)
                    buffer += " " + sentence;
                else
                {
                    if (!string.IsNullOrWhiteSpace(buffer))
                        result.Add(buffer);
                    buffer = sentence;
                }
            }

            if (!string.IsNullOrWhiteSpace(buffer))
                result.Add(buffer);

            return result;
        }

        // =====================================================================
        // TÌM LOCALE PHÙ HỢP TRÊN THIẾT BỊ
        // =====================================================================
        /// <summary>
        /// Tìm giọng đọc TTS tốt nhất trên thiết bị cho ngôn ngữ yêu cầu.
        /// Logic: thử từng prefix theo thứ tự ưu tiên → nếu không có → fallback EN → null (hệ thống tự chọn).
        /// </summary>
        private async Task<Locale?> FindBestLocaleAsync(string languageCode)
        {
            try
            {
                var allLocales = await TextToSpeech.Default.GetLocalesAsync();
                if (!allLocales.Any()) return null;

                // Lấy danh sách prefix cần tìm cho ngôn ngữ này
                var prefixes = LocalePrefixes.GetValueOrDefault(languageCode, new[] { languageCode });

                // Tìm locale khớp prefix theo thứ tự ưu tiên
                foreach (var prefix in prefixes)
                {
                    var match = allLocales.FirstOrDefault(l =>
                        l.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TTS] Tìm được locale: {match.Language} cho '{languageCode}'");
                        return match;
                    }
                }

                // Fallback: nếu không có ngôn ngữ yêu cầu (ví dụ Khmer trên thiết bị không có)
                // Thử dùng tiếng Anh (phổ biến nhất trên mọi thiết bị)
                if (languageCode != "en")
                {
                    var enLocale = allLocales.FirstOrDefault(l =>
                        l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));
                    if (enLocale != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[TTS] Không có giọng '{languageCode}', dùng EN fallback");
                        return enLocale;
                    }
                }

                // Fallback cuối cùng: để hệ thống tự chọn giọng mặc định
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] GetLocales lỗi: {ex.Message}");
                return null;
            }
        }
    }
}
