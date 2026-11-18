using MDFileViewer.Core.Models;

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

    /// <summary>문서 세로 위치를 비율(0~1)로 맞춘다 — 편집기와 미리보기 스크롤 동기화 (01 F-09, 02 §17.1).</summary>
    Task ScrollToRatioAsync(double ratio, CancellationToken cancellationToken);

    /// <summary>문서 테마를 바꾼다. 문서를 아직 보여 주지 않았으면 기억해 두었다가 처음 보여 줄 때 쓴다 (02 §6.6·§17.3).</summary>
    Task ApplyThemeAsync(EffectiveTheme theme, CancellationToken cancellationToken);

    /// <summary>표시 배율을 넣는다. 뷰어를 다시 만들어도 이 값을 유지한다 (01 F-06, 02 §17.2).</summary>
    void SetZoomFactor(double factor);

    /// <summary>사용자가 `Ctrl+휠`로 배율을 바꿨다. <see cref="SetZoomFactor"/>로 넣은 값은 알리지 않는다.</summary>
    event EventHandler<double>? ZoomFactorChanged;
}

/// <summary>눌린 링크의 절대 URL(&lt;base href&gt; 기준으로 풀린 값).</summary>
public sealed class LinkActivatedEventArgs(string url) : EventArgs
{
    public string Url { get; } = url;
}
