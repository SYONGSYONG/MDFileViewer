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

    // 2026-11-18 SegoeIcons.ttf로 그려 모양을 확인했다(이름은 Fluent Icons 목록 이름)
    public static readonly string Edit = Glyph(0xE70F);
    public static readonly string Save = Glyph(0xE74E);
    public static readonly string ZoomIn = Glyph(0xE8A3);
    public static readonly string ZoomOut = Glyph(0xE71F);
    public static readonly string Theme = Glyph(0xE790);
    public static readonly string Help = Glyph(0xE897);
    public static readonly string More = Glyph(0xE712);
    public static readonly string FavoriteStar = Glyph(0xE734);
    public static readonly string ChevronRight = Glyph(0xE76C);
    public static readonly string Close = Glyph(0xE711);

    // 2026-11-18 같은 방법으로 확인: 왼쪽 칸이 나뉜 창 모양(DockLeft) — 접기·펴기 어느 상태든 한 아이콘으로 토글한다
    public static readonly string NavigationPanel = Glyph(0xE90C);

    private static string Glyph(int codePoint) => char.ConvertFromUtf32(codePoint);
}
