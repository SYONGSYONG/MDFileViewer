namespace MDFileViewer.Core.Abstractions;

/// <summary>문서 표시 영역(WebView2). App의 WebViewHost가 구현한다 (02 §4.1, §6.2).</summary>
public interface IViewerHost
{
    /// <summary>사용자가 문서 안 링크를 눌렀다. 문서가 스스로 일으킨 이동은 알리지 않는다 (02 §6.4).</summary>
    event EventHandler<LinkActivatedEventArgs>? LinkActivated;

    /// <summary>새 문서를 보여 주고 맨 위로 스크롤한다. 상대 경로는 <paramref name="documentPath"/> 기준으로 푼다.</summary>
    Task ShowDocumentAsync(string bodyHtml, string documentPath, CancellationToken cancellationToken);

    /// <summary>같은 문서의 본문만 바꾼다. 스크롤 위치를 유지한다(새로고침, 편집 미리보기).</summary>
    Task UpdateBodyAsync(string bodyHtml, CancellationToken cancellationToken);

    Task ScrollToAnchorAsync(string anchorId, CancellationToken cancellationToken);
}

/// <summary>눌린 링크의 절대 URL(&lt;base href&gt; 기준으로 풀린 값).</summary>
public sealed class LinkActivatedEventArgs(string url) : EventArgs
{
    public string Url { get; } = url;
}
