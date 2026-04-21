using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// TtsEngine — phát TTS an toàn, không bao giờ crash app.
    ///
    /// CÁC LỖI ĐÃ FIX:
    /// 1. OperationCanceledException không được catch → crash
    /// 2. GetLocalesAsync() throw exception trên một số thiết bị → crash
    /// 3. SpeakAsync trên thiết bị không có giọng đọc → crash
    /// 4. Gọi StopAsync() khi không có gì đang phát → NullReferenceException
    /// </summary>
    public class TtsEngine : IDisposable
    {
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public bool IsSpeaking { get; private set; }

        private static readonly Dictionary<string, string[]> LocalePrefixes = new()
        {
            ["vi"] = ["vi"],
            ["en"] = ["en-US"],
            ["th"] = ["th"],
            ["id"] = ["id"],
            ["ms"] = ["ms"],
            ["km"] = ["km"],
        };

        public async Task SpeakPoiAsync(Poi poi, string lang)
        {
            // Ngăn chặn nhiều lần gọi đồng thời.
            // Timeout 3s (trước là 100ms → quá ngắn: khi chuyển giữa 2 POI trong
            // hàng đợi, nếu có bất kỳ op nào giữ lock trong ~200ms là POI mới
            // bị bỏ qua im lặng, không đọc được gì).
            if (!await _lock.WaitAsync(3000))
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Đang bận, bỏ qua");
                return;
            }

            try
            {
                await StopInternalAsync();

                _cts = new CancellationTokenSource();
                IsSpeaking = true;

                var sentences = BuildScript(poi, lang);
                var locale = await FindLocaleAsync(lang);

                // Nếu không tìm được locale phù hợp, vẫn phát với locale mặc định
                var opts = new SpeechOptions
                {
                    Pitch = lang == "th" ? 1.05f : 1.0f,
                    Volume = 1.0f
                };
                if (locale != null) opts.Locale = locale;

                System.Diagnostics.Debug.WriteLine(
                    $"[TTS] Phát {sentences.Count} câu | lang={lang} | locale={locale?.Language ?? "default"}");

                foreach (var sentence in sentences)
                {
                    if (_cts == null || _cts.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(sentence)) continue;

                    try
                    {
                        await TextToSpeech.Default.SpeakAsync(sentence, opts, _cts.Token);
                        // Delay giữa câu (tạo nhịp tự nhiên)
                        if (_cts != null && !_cts.IsCancellationRequested)
                            await Task.Delay(300, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("[TTS] Bị cancel giữa câu");
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Lỗi một câu → bỏ qua, phát câu tiếp (không crash toàn bộ)
                        System.Diagnostics.Debug.WriteLine($"[TTS] Lỗi câu: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
                // KHÔNG rethrow — để NarrationEngine xử lý gracefully
            }
            finally
            {
                IsSpeaking = false;
                _lock.Release();
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await StopInternalAsync();
                // Đợi lock rảnh trước khi return
                if (await _lock.WaitAsync(200))
                    _lock.Release();
            }
            catch { }
        }

        private async Task StopInternalAsync()
        {
            IsSpeaking = false;
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    await Task.Delay(50); // Cho SpeakAsync nhận cancel signal
                }
                _cts?.Dispose();
                _cts = null;
            }
            catch { }
            //TextToSpeech.Default.C
            //try { TextToSpeech.Default.CancelAll(); }
            //catch { }
           
        }

        // ── Script 3 phần ─────────────────────────────────────────────────
        private static List<string> BuildScript(Poi poi, string lang)
        {
            var result = new List<string>();
            if (!string.IsNullOrWhiteSpace(poi.Name))
                result.Add(Welcome(poi.Name, lang));

            if (!string.IsNullOrWhiteSpace(poi.Description))
                result.AddRange(SplitSentences(poi.Description));

            result.Add(Closing(lang));
            return result.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        private static string Welcome(string name, string lang) => lang switch
        {
            "en" => $"Welcome! You are now at {name}.",
            "th" => $"ยินดีต้อนรับ! คุณอยู่ที่ {name}",
            "id" => $"Selamat datang di {name}.",
            "ms" => $"Selamat datang ke {name}.",
            "km" => $"សូមស្វាគមន៍ {name}",
            _ => $"Chào mừng bạn đến với {name}!",
        };

        private static string Closing(string lang) => lang switch
        {
            "en" => "Thank you for visiting Saigon Zoo. Enjoy your day!",
            "th" => "ขอบคุณที่มาเยือนสวนสัตว์ไซง่อน!",
            "id" => "Terima kasih telah berkunjung. Selamat menikmati!",
            "ms" => "Terima kasih kerana berkunjung!",
            "km" => "អរគុណដែលបានមក!",
            _ => "Cảm ơn bạn đã tham quan Thảo Cầm Viên Sài Gòn. Chúc một ngày vui vẻ!",
        };

        private static List<string> SplitSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return [];

            var parts = text.Split([". ", "! ", "? "], StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();

            foreach (var part in parts)
            {
                var s = part.Trim();
                if (string.IsNullOrWhiteSpace(s) || s.Length < 5) continue;
                if (!s.EndsWith('.') && !s.EndsWith('!') && !s.EndsWith('?'))
                    s += ".";
                result.Add(s);
            }

            // Nếu text không có dấu chấm → trả về nguyên văn
            if (result.Count == 0)
                result.Add(text.Trim());

            return result;
        }

        // ── Tìm locale an toàn ────────────────────────────────────────────
        private static async Task<Locale?> FindLocaleAsync(string lang)
        {
            try
            {
                var all = (await TextToSpeech.Default.GetLocalesAsync())?.ToList();
                if (all == null || all.Count == 0) return null;

                var prefixes = LocalePrefixes.GetValueOrDefault(lang, [lang]);

                // Tìm locale khớp
                foreach (var prefix in prefixes)
                {
                    var match = all.FirstOrDefault(l =>
                        l.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }

                // Fallback: tiếng Anh (có trên mọi thiết bị Android/iOS)
                return all.FirstOrDefault(l =>
                    l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] GetLocales failed: {ex.Message}");
                return null; // Trả về null → dùng giọng mặc định của hệ thống
            }
        }

        public void Dispose()
        {
            _ = StopAsync();
            _lock.Dispose();
        }
    }
}
