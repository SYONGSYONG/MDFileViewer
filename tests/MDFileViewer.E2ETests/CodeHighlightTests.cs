using System.Drawing;
using FlaUI.Core.Capturing;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 코드 블록 구문 색상 E2E (01 F-22, 05 12-3). 입력을 보내지 않고, 색은 화면의 한 점을 찍어 확인한다 — UIA로는 글자색을 알 수 없다.
/// </summary>
public sealed class CodeHighlightTests : IDisposable
{
    private const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 화면을 찍지 않습니다.";
    private const string FileName = "highlight-sample.md";
    private const string Heading = "구문 색상 견본";

    // 칸을 꽉 채우는 글자(U+2588)로 된 주석 한 줄 — 글자 가운데 한 점이 가장자리 번짐 없이 글자색이다
    private const string CommentLine = "// ████████████████";

    // 주석 줄에서 색을 찍을 가로 위치(왼쪽부터의 비율). 앞의 "// "를 지나 꽉 찬 글자 위다
    private const double ProbeRatio = 0.75;

    // 디스플레이 색 관리로 화면 색이 조금 달라진다(ViewerRenderingTests 참고). 주석색과 본문색의 차이(밝게 61 이상, 어둡게 59 이상)보다 작게 둔다
    private const int ColorTolerance = 25;

    // 03 §6.1 `Code.Comment` — 테마는 시스템 설정을 따르므로 둘 중 하나면 된다
    private static readonly Color LightComment = Color.FromArgb(0x5B, 0x62, 0x70);
    private static readonly Color DarkComment = Color.FromArgb(0x9A, 0xA3, 0xAF);

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void FencedCodeWithLanguage_IsColoredShortlyAfterDocumentIsShown()
    {
        // Arrange
        var path = _samples.PathOf(FileName);
        File.WriteAllText(path, $"# {Heading}\n\n```csharp\n{CommentLine}\n```\n");

        // Act — 문서는 색 없이 먼저 보이고(제목이 보임), 색은 뒤에 입혀진다
        using var session = AppSession.Launch(path);
        session.FindByName(Heading);

        // Assert
        AppSession.WaitUntil(
            () => IsCommentColor(CommentProbe(session)),
            "코드 블록의 주석이 주석색(03 §6.1 Code.Comment)으로 바뀌지 않았습니다.");

        // 본문을 바꾼 뒤에도 문서는 그대로다
        session.FindByName(Heading);
    }

    private static Color CommentProbe(AppSession session)
    {
        Assert.True(session.IsForeground, $"{NotForegroundMessage} 맨 앞 창: {AppSession.DescribeForegroundWindow()}");
        var comment = session.FindByName(CommentLine);
        using var image = Capture.Element(comment);
        return image.Bitmap.GetPixel((int)(image.Bitmap.Width * ProbeRatio), image.Bitmap.Height / 2);
    }

    private static bool IsCommentColor(Color actual) => IsNear(actual, LightComment) || IsNear(actual, DarkComment);

    private static bool IsNear(Color actual, Color expected) =>
        Math.Abs(actual.R - expected.R) <= ColorTolerance
        && Math.Abs(actual.G - expected.G) <= ColorTolerance
        && Math.Abs(actual.B - expected.B) <= ColorTolerance;
}
