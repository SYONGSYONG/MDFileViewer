using System.Text.Json;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// viewer.html에 `ExecuteScriptAsync`로 보낼 스크립트를 만든다 (02 §6.2 안 A). 문서 안 스크립트는 꺼져 있어도
/// 호스트가 보내는 이 스크립트는 실행된다(PoC-1). 값은 모두 JSON 문자열로 넣어 따옴표·줄 바꿈·비ASCII로 문법이 깨지지 않게 한다.
/// </summary>
/// <remarks>
/// 문서 본문의 원시 HTML이 DOM 속성을 덮을 수 있다(DOM clobbering): `&lt;img name="getElementById"&gt;`가 있으면
/// `document.getElementById`가 그 요소가 되어 다음 문서를 열 수 없게 되고, `&lt;img name="documentElement"&gt;`도 같다
/// (Chromium에서 확인, 2026-11-17). 그래서 `document.x` 대신 덮을 수 없는 프로토타입의 메서드를 `call`로 부른다.
/// 전역 생성자(`Document`·`Element`·`Object`)와 `window.scrollTo`는 같은 방법으로 덮이지 않음을 함께 확인했다.
/// </remarks>
public static class ViewerScriptBuilder
{
    /// <summary>viewer.html의 &lt;base&gt; 요소 id. 문서 URL로 바꿔 상대 이미지·앵커를 문서 기준으로 푼다.</summary>
    public const string BaseElementId = "docbase";

    /// <summary>viewer.html에서 본문 HTML을 넣는 요소 id.</summary>
    public const string ContentElementId = "content";

    private const string ScrollToTop = "window.scrollTo(0,0);";

    private const string DocumentElement =
        "Object.getOwnPropertyDescriptor(Document.prototype,\"documentElement\").get.call(document)";

    /// <summary>새 문서를 보여 준다. 이미지가 새 문서 기준으로 풀리도록 &lt;base&gt;를 본문보다 먼저 바꾸고, 맨 위로 스크롤한다.</summary>
    public static string ShowDocument(string bodyHtml, string documentUrl) =>
        $"{GetElementById(BaseElementId)}.href={Literal(documentUrl)};" +
        SetContent(bodyHtml) +
        ScrollToTop;

    /// <summary>같은 문서의 본문만 바꾼다. 스크롤 위치는 그대로 둔다(편집 미리보기, 01 F-09).</summary>
    public static string UpdateBody(string bodyHtml) => SetContent(bodyHtml);

    /// <summary>id가 같은 요소로 스크롤한다. 빈 id는 문서 처음으로 간다. 없는 id는 아무것도 하지 않는다.</summary>
    public static string ScrollToAnchor(string anchorId)
    {
        ArgumentNullException.ThrowIfNull(anchorId);

        // 대상이 폼이면 `<input name="scrollIntoView">`가 요소의 메서드도 덮으므로 프로토타입으로 부른다
        return anchorId.Length == 0
            ? ScrollToTop
            : $"(function(){{var e={GetElementById(anchorId)};if(e){{Element.prototype.scrollIntoView.call(e);}}}})();";
    }

    /// <summary>문서 테마를 바꾼다. viewer.css가 `data-theme` 값으로 색 토큰을 고른다 (02 §6.6).</summary>
    public static string SetTheme(EffectiveTheme theme)
    {
        var value = theme switch
        {
            EffectiveTheme.Light => "light",
            EffectiveTheme.Dark => "dark",
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "알 수 없는 테마입니다."),
        };
        return $"Element.prototype.setAttribute.call({DocumentElement},{Literal("data-theme")},{Literal(value)});";
    }

    /// <summary>
    /// 문서 세로 위치를 비율로 맞춘다(편집기 스크롤 동기화, 01 F-09). 0은 맨 위, 1은 맨 아래다. 범위 밖 값은 자른다.
    /// </summary>
    public static string ScrollToRatio(double ratio)
    {
        if (double.IsNaN(ratio))
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "비율이 숫자가 아닙니다.");
        }

        var value = Math.Clamp(ratio, 0, 1).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        return $"(function(){{var r={DocumentElement};window.scrollTo(0,{value}*Math.max(0,r.scrollHeight-window.innerHeight));}})();";
    }

    // 템플릿의 <base>·<main>은 본문보다 앞에 있으므로, 본문에 같은 id가 있어도 문서 순서상 먼저인 템플릿 요소가 잡힌다
    private static string SetContent(string bodyHtml) =>
        $"{GetElementById(ContentElementId)}.innerHTML={Literal(bodyHtml)};";

    private static string GetElementById(string id) =>
        $"Document.prototype.getElementById.call(document,{Literal(id)})";

    // 기본 인코더는 비ASCII·`<`·`'`·U+2028을 \uXXXX로 바꾼다. 스크립트에 날것으로 들어가면 문자열이 끊길 수 있는 문자들이다.
    private static string Literal(string value) => JsonSerializer.Serialize(value);
}
