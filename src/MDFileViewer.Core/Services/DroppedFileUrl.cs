namespace MDFileViewer.Core.Services;

/// <summary>
/// 뷰어(WebView2) 위에 놓은 파일·폴더는 창의 드롭 이벤트로 오지 않고, WebView2가 항목마다 `file:` 주소의 새 창 요청으로 알린다
/// (02 §11 PoC-7). 그 주소를 로컬 경로로 바꾼다.
/// </summary>
public static class DroppedFileUrl
{
    /// <returns><paramref name="url"/>이 `file:` 주소면 <c>true</c>와 로컬 경로(UNC 포함). 아니면 <c>false</c>와 빈 문자열.</returns>
    public static bool TryGetLocalPath(string url, out string path)
    {
        path = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return false;
        }

        // LocalPath가 퍼센트 인코딩을 풀고 구분자를 바꾼다(`file://server/share/…`는 UNC 경로로)
        path = uri.LocalPath;
        return path.Length > 0;
    }
}
