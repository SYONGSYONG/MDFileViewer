namespace MDFileViewer.Core.Models;

/// <summary>사용자가 고른 테마 (01 F-11). 설정 파일에는 이름(`System`·`Light`·`Dark`)으로 저장한다.</summary>
public enum ThemeMode
{
    /// <summary>Windows 앱 모드를 따른다(기본).</summary>
    System,
    Light,
    Dark,
}
