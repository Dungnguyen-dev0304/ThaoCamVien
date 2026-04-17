using System.Globalization;
using AppThaoCamVien.Services;

namespace AppThaoCamVien.Converters;

/// <summary>
/// Chuyển giá trị Poi.ImageThumbnail (tên file thuần hoặc URL) → URL tuyệt đối
/// trỏ đến wwwroot/images/pois của WebThaoCamVien (port 5181).
///
/// Dùng trong XAML:
///   &lt;ContentPage.Resources&gt;
///     &lt;conv:PoiImageUrlConverter x:Key="PoiImgConv" /&gt;
///   &lt;/ContentPage.Resources&gt;
///   &lt;Image Source="{Binding ImageThumbnail, Converter={StaticResource PoiImgConv}}" /&gt;
///
/// Rỗng → trả về null (MAUI Image sẽ hiện state mặc định, không crash).
/// </summary>
public sealed class PoiImageUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return null;

        var url = MediaUrlResolver.ImageUrlFor(s);
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
