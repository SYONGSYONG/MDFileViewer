using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Themes;

namespace MDFileViewer.Controls;

/// <summary>트리 노드 아이콘: [종류, 펼침 여부, 접근 결과] → 글리프 (03 §9).</summary>
public sealed class FolderGlyphConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [FolderKind kind, bool isExpanded, FolderAccess access])
        {
            return IconGlyphs.Folder;
        }

        return kind switch
        {
            FolderKind.ThisPc => IconGlyphs.ThisPc,
            FolderKind.LocalDrive => IconGlyphs.HardDrive,
            FolderKind.RemovableDrive => IconGlyphs.RemovableDrive,
            FolderKind.NetworkDrive => IconGlyphs.NetworkDrive,
            FolderKind.OpticalDrive => IconGlyphs.OpticalDrive,
            _ when access == FolderAccess.AccessDenied => IconGlyphs.Lock,
            _ => isExpanded ? IconGlyphs.FolderOpen : IconGlyphs.Folder,
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 표시 변환기의 매개변수. XAML에서 <c>{x:Static controls:VisibilityParameters.Hidden}</c>로 써서 철자가 틀리면 빌드가 실패하게 한다.
/// </summary>
public static class VisibilityParameters
{
    /// <summary>조건을 뒤집는다.</summary>
    public const string Invert = "Invert";

    /// <summary>보이지 않을 때 접지 않고 숨긴다(자리를 유지한다).</summary>
    public const string Hidden = "Hidden";

    /// <summary>두 변환기가 같은 방식으로 읽도록 포함 여부로 본다. 여러 개는 이어 써도 된다(예: <c>"Invert Hidden"</c>).</summary>
    internal static bool Has(object? parameter, string flag) =>
        parameter is string text && text.Contains(flag, StringComparison.Ordinal);
}

/// <summary>값이 있으면 보이고 <c>null</c>·빈 문자열이면 접는다. 매개변수 <see cref="VisibilityParameters.Invert"/>면 반대.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not string { Length: 0 };
        return VisibilityFor(hasValue, parameter);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Visibility VisibilityFor(bool isVisible, object? parameter) =>
        isVisible ^ VisibilityParameters.Has(parameter, VisibilityParameters.Invert) ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// bool → Visibility. 매개변수 <see cref="VisibilityParameters.Invert"/>면 반대, <see cref="VisibilityParameters.Hidden"/>이 들어 있으면
/// 접지 않고 숨긴다 — WebView2(HwndHost)는 숨겨도 자리와 창 핸들을 유지해야 문서 표시 준비가 이어진다.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isVisible = value is true ^ VisibilityParameters.Has(parameter, VisibilityParameters.Invert);
        if (isVisible)
        {
            return Visibility.Visible;
        }

        return VisibilityParameters.Has(parameter, VisibilityParameters.Hidden) ? Visibility.Hidden : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>바이트 수 → `4.2 KB` (Core <see cref="DisplayFormat"/>와 같은 모양).</summary>
public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long size ? DisplayFormat.FormatSize(size) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>수정 시각 → PC 현지 시간 `2026-11-17 15:30`.</summary>
public sealed class LocalDateTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset time ? DisplayFormat.FormatDateTime(time, TimeZoneInfo.Local) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
