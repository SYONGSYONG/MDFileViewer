namespace MDFileViewer.Core.Models;

/// <summary>지난 실행에서 저장해 둔 화면 상태 (01 F-14·F-17). 인수 없이 시작할 때만 되살린다.</summary>
public sealed record SessionState(string? LastFolder, TabsSettings Tabs)
{
    public static SessionState Empty { get; } = new(null, TabsSettings.Empty);
}
