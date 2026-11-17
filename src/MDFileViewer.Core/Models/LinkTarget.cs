namespace MDFileViewer.Core.Models;

/// <summary>문서 안 링크를 클릭했을 때 할 일을 정하는 분류 결과 (01 F-05, 02 §6.4).</summary>
public abstract record LinkTarget
{
    private LinkTarget()
    {
    }

    /// <summary>웹·메일 링크 — 기본 브라우저·메일 앱으로 연다.</summary>
    public sealed record External(Uri Uri) : LinkTarget;

    /// <summary>현재 문서 안의 앵커. 빈 문자열이면 문서 처음으로 간다.</summary>
    public sealed record CurrentDocumentAnchor(string AnchorId) : LinkTarget;

    /// <summary>다른 md 파일 — 앱 안에서 열고, 앵커가 있으면 그 위치로 스크롤한다.</summary>
    public sealed record MarkdownDocument(string FilePath, string? AnchorId) : LinkTarget;

    /// <summary>md가 아닌 파일이나 폴더 — 실행하지 않고 탐색기에서 선택해 보여 준다.</summary>
    public sealed record LocalFile(string Path) : LinkTarget;

    /// <summary>문서 루트 안을 가리키지만 없는 파일·폴더.</summary>
    public sealed record MissingFile(string Path) : LinkTarget;

    /// <summary>처리하지 않는 링크(`javascript:`, `file:`, `data:`, 앱 내부 호스트, 해석할 수 없는 URL 등).</summary>
    public sealed record Blocked(string RawUrl) : LinkTarget;
}
