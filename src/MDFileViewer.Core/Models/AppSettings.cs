namespace MDFileViewer.Core.Models;

/// <summary>
/// 설정 파일 형식 (02 §10, 01 F-14). 없는 필드는 기본값을 쓴다. 값 검사는 <see cref="Services.JsonSettingsStore"/>가 읽을 때 한다.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>마지막 창 위치·크기(DIP). <c>null</c>이면 시작 창 맞춤(02 §4.2)을 쓴다.</summary>
    public WindowSettings? Window { get; init; }

    public LayoutSettings Layout { get; init; } = new();

    public ThemeMode Theme { get; init; } = ThemeMode.System;

    public string? LastFolder { get; init; }

    public EditorSettings Editor { get; init; } = new();

    public IReadOnlyList<string> Favorites { get; init; } = [];

    public TabsSettings Tabs { get; init; } = TabsSettings.Empty;

    /// <summary>
    /// 탐색기 메뉴가 다른 exe로 등록돼 있을 때 재등록 제안을 "나중에"로 거절한 exe 경로(2026-11-18 사용자 결정). 이 exe로 시작하면 다시 묻지 않는다.
    /// </summary>
    public string? DeclinedShellReregistrationExe { get; init; }
}

public sealed record WindowSettings(double Left, double Top, double Width, double Height, bool IsMaximized);

public sealed record LayoutSettings
{
    public double TreePanelWidth { get; init; } = AppConstants.DefaultTreePanelWidth;

    public double EditorSplitRatio { get; init; } = AppConstants.DefaultEditorSplitRatio;
}

public sealed record EditorSettings
{
    public bool ShowLineNumbers { get; init; } = true;

    public bool WordWrap { get; init; } = true;
}
