using GTranslate.Translators;

namespace AppThaoCamVien.Services
{
    public class AutoTranslateService
    {
        // Khởi tạo thư viện: AggregateTranslator sẽ tự động dùng Google, Bing, Yandex... cái nào sống thì nó dùng
        private readonly AggregateTranslator _translator = new AggregateTranslator();

        /// <summary>
        /// Hàm dịch tự động bằng thư viện GTranslate
        /// </summary>
        public async Task<string> TranslateAsync(string text, string toLanguageCode)
        {
            // Nếu chữ rỗng hoặc đang ở tiếng Việt thì không cần dịch
            if (string.IsNullOrWhiteSpace(text) || toLanguageCode == "vi")
                return text;

            try
            {
                // Thư viện tự động phát hiện ngôn ngữ gốc và dịch sang ngôn ngữ đích
                var result = await _translator.TranslateAsync(text, toLanguageCode);

                // Trả về kết quả đã dịch
                return result.Translation;
            }
            catch (Exception ex)
            {
                // Nếu rớt mạng hoặc thư viện lỗi, trả về nguyên gốc tiếng Việt để App không bị Crash
                System.Diagnostics.Debug.WriteLine($"[Thư viện Dịch Lỗi]: {ex.Message}");
                return text;
            }
        }
    }
}