namespace ApiThaoCamVien;

/// <summary>
/// Web admin lưu file ảnh tại WebThaoCamVien/wwwroot/images/pois/ và chỉ ghi tên file vào DB.
/// API phải trả về URL tuyệt đối để app MAUI tải ảnh qua HTTP (cùng host với static files).
/// </summary>
public static class PoiMediaUrls
{
    public static string ResolveThumbnail(HttpRequest request, string? thumbnail)
    {
        if (string.IsNullOrWhiteSpace(thumbnail))
            return "";

        var t = thumbnail.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return t;

        var fileName = Path.GetFileName(t);
        if (string.IsNullOrEmpty(fileName))
            return "";

        var host = request.Host.Value;
        return $"{request.Scheme}://{host}/images/pois/{Uri.EscapeDataString(fileName)}";
    }
}
