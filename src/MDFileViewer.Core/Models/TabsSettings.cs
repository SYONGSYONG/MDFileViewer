namespace MDFileViewer.Core.Models;

/// <summary>설정에 저장하는 탭 목록과 활성 탭 위치 (01 F-17 복원). 탭이 없으면 <see cref="ActiveIndex"/>는 -1이다.</summary>
public sealed record TabsSettings(IReadOnlyList<TabSettings> Files, int ActiveIndex)
{
    public static TabsSettings Empty { get; } = new([], -1);
}
