using System.Drawing;
using System.Text;

namespace MDFileViewer.E2ETests;

/// <summary>
/// E2E용 샘플 폴더(`%TEMP%\MDFileViewer.E2E\&lt;guid&gt;\문서 폴더`). 한글·공백이 든 경로로 만들어 경로 인코딩도 함께 확인한다.
/// </summary>
public sealed class SampleDocuments : IDisposable
{
    public const string FolderName = "문서 폴더";
    public const string GuideFileName = "guide.md";
    public const string OtherFileName = "other.md";
    public const string RefreshFileName = "refresh.md";
    public const string ClobberFileName = "clobber.md";
    public const string GuideHeading = "가이드 문서";
    public const string OtherHeading = "다른 문서";
    public const string ClobberHeading = "DOM 덮어쓰기 문서";
    public const string RefreshHeading = "meta refresh 문서";
    public const string RefreshMetaTag = "<meta http-equiv=\"refresh\" content=\"0;url=" + OtherFileName + "\">";
    public const string OtherDocumentLink = "다른 문서로 이동";
    public const string MissingFileLink = "없는 파일";
    public const string AnchorLink = "아래 제목으로";
    public const string AnchorTargetHeading = "코드 블록";
    public const string MissingFileName = "missing.md";
    public const string PictureAltText = "상대 이미지";

    /// <summary>샘플 폴더 바로 아래 md 파일 — 트리에서 하위 폴더 다음에 이 순서(이름순)로 나온다.</summary>
    public static readonly IReadOnlyList<string> RootMarkdownFiles = [ClobberFileName, GuideFileName, OtherFileName, RefreshFileName];

    /// <summary>샘플 폴더 바로 아래 하위 폴더 — md 파일이 없는 images도 트리에 보인다(01 F-02).</summary>
    public static readonly IReadOnlyList<string> RootFolders = ["images", "하위", DeepFolderName];

    // 트리에 가로 스크롤이 생기도록 이름이 긴 폴더를 두 단계 둔다(01 F-02 가로 스크롤 E2E).
    // 트리 기본 폭(320px)보다 이 행 하나가 넓다.
    public const string DeepFolderName = "가로 스크롤이 생기도록 이름을 아주 길게 지은 첫 번째 폴더";
    public const string DeeperFolderName = "가로 스크롤이 생기도록 이름을 아주 길게 지은 두 번째 폴더";
    public const string DeepFileName = "deep.md";

    /// <summary>실행 중에 새로 만드는 파일(새로고침 E2E). 이름순으로 guide.md 앞에 와 그 폴더의 자식 목록이 바뀐다.</summary>
    public const string AddedFileName = "added.md";
    public const string DeepHeading = "깊은 문서";

    /// <summary>깊은 폴더의 md 파일 수(deep.md 제외). 화면 높이보다 행이 많아 키보드로 내려가면 세로 스크롤이 필요하다.</summary>
    public const int DeepNoteCount = 40;

    /// <summary>guide.md의 상대 경로 그림(images/pic.svg)을 채운 단색. 화면에서 이 색이 보이면 그림을 읽어 온 것이다.</summary>
    public static readonly Color PictureColor = Color.FromArgb(0x2F, 0x6F, 0xDE);

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root;

    public SampleDocuments()
        : this(Path.GetTempPath())
    {
    }

    /// <param name="baseDirectory">샘플을 만들 위치. 다른 드라이브의 문서가 필요할 때(문서 루트 전환) 바꾼다.</param>
    public SampleDocuments(string baseDirectory)
    {
        _root = Path.Combine(baseDirectory, "MDFileViewer.E2E", Guid.NewGuid().ToString("N"));
        FolderPath = Path.Combine(_root, FolderName);
        Directory.CreateDirectory(Path.Combine(FolderPath, "images"));
        Directory.CreateDirectory(Path.Combine(FolderPath, "하위"));
        Write(GuideFileName, GuideMarkdown);
        Write(OtherFileName, $"# {OtherHeading}\n\n## 첫 번째\n\n내용\n\n## 두 번째\n\n도착했습니다.\n");
        Write(RefreshFileName, RefreshMarkdown);
        Write(ClobberFileName, ClobberMarkdown);
        Write(Path.Combine("images", "pic.svg"), PictureSvg);
        Write(Path.Combine("하위", "sub.md"), "# 하위 문서\n");
        Directory.CreateDirectory(Path.Combine(FolderPath, DeepFolderName, DeeperFolderName));
        Write(Path.Combine(DeepFolderName, DeeperFolderName, DeepFileName), $"# {DeepHeading}\n");
        for (var number = 1; number <= DeepNoteCount; number++)
        {
            Write(Path.Combine(DeepFolderName, DeeperFolderName, DeepNoteName(number)), $"# 메모 {number}\n");
        }
    }

    public string FolderPath { get; }

    public string DeepFilePath => Path.Combine(FolderPath, DeepFolderName, DeeperFolderName, DeepFileName);

    /// <summary>이름순으로 deep.md 다음에 온다.</summary>
    public static string DeepNoteName(int number) => $"note-{number:D2}.md";

    public string PathOf(string relativePath) => Path.Combine(FolderPath, relativePath);

    /// <summary>샘플 폴더에 md 파일을 새로 만든다.</summary>
    public void AddMarkdownFile(string fileName) => Write(fileName, $"# {Path.GetFileNameWithoutExtension(fileName)}\n");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Write(string relativePath, string content) =>
        File.WriteAllText(Path.Combine(FolderPath, relativePath), content, Utf8WithoutBom);

    // 위쪽 여백을 많이 둬 앵커 이동 전에는 "코드 블록" 제목이 화면 아래쪽에 있게 한다
    private static string GuideMarkdown => $"""
        # {GuideHeading}

        - [{AnchorLink}](#코드-블록)
        - [{OtherDocumentLink}]({OtherFileName}#두-번째)
        - [{MissingFileLink}]({MissingFileName})

        ![{PictureAltText}](images/pic.svg)

        {string.Concat(Enumerable.Repeat("문단입니다.\n\n", 60))}
        ## {AnchorTargetHeading}

        ```
        var x = 1;
        ```

        <script>document.title='pwned'</script>
        """;

    // 문서를 열기만 해도 클릭 없이 탐색이 일어난다. 앱은 이것을 링크로 처리하면 안 된다 (02 §6.4, 05 4-6).
    // 보호가 깨져도 사용자 브라우저가 열리지 않도록 외부 주소 대신 같은 폴더의 md로 이동시킨다 — 깨지면 제목이 other.md로 바뀐다.
    private static string RefreshMarkdown => $"""
        # {RefreshHeading}

        {RefreshMetaTag}
        """;

    // 문서 원시 HTML이 document의 속성을 덮어도 다음 문서를 열 수 있어야 한다 (02 §6.2 DOM clobbering, 05 4-4)
    private static string ClobberMarkdown => $"""
        # {ClobberHeading}

        <img name="getElementById" src="images/pic.svg">
        <img name="documentElement" src="images/pic.svg">
        <form><input name="scrollIntoView"></form>
        """;

    private static string PictureSvg =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"60\"><rect width=\"120\" height=\"60\" fill=\""
        + $"#{PictureColor.R:X2}{PictureColor.G:X2}{PictureColor.B:X2}" + "\"/></svg>";
}
