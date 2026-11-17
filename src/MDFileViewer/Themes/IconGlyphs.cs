namespace MDFileViewer.Themes;

/// <summary>
/// Segoe Fluent Icons 글리프 (03 §9). 코드 값은 Microsoft Learn "Segoe Fluent Icons font" 목록(1.56, 2026-10-13판)에서 확인했다
/// (2026-11-17). Windows 10에는 이 글꼴이 기본으로 없어 Segoe MDL2 Assets로 대체되며, 두 글꼴의 코드 값이 같은지는 문서에 없다
/// — Phase 11 Windows 10 확인 항목.
/// </summary>
/// <remarks>사용자 정의 영역 문자는 소스에서 눈에 보이지 않고 도구가 이스케이프를 풀어 버리므로 코드 값으로 만든다(04 §4).</remarks>
public static class IconGlyphs
{
    public const string FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    public static readonly string ThisPc = Glyph(0xEC4E);
    public static readonly string HardDrive = Glyph(0xEDA2);
    public static readonly string RemovableDrive = Glyph(0xE88E);
    public static readonly string NetworkDrive = Glyph(0xE8CE);
    public static readonly string OpticalDrive = Glyph(0xE958);
    public static readonly string Folder = Glyph(0xE8B7);
    public static readonly string FolderOpen = Glyph(0xE838);
    public static readonly string Lock = Glyph(0xE72E);
    public static readonly string OpenFolder = Glyph(0xE8DA);
    public static readonly string Refresh = Glyph(0xE72C);
    public static readonly string Document = Glyph(0xE8A5);

    private static string Glyph(int codePoint) => char.ConvertFromUtf32(codePoint);
}
