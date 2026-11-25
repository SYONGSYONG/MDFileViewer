using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>인쇄, PDF·HTML로 내보내기 명령 (01 F-26, 02 §17.17).</summary>
public sealed class MainViewModelExportTests : IDisposable
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Stylesheet = "body { margin: 0; }";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.Zero));
    private readonly string _outputFolder = Path.Combine(Path.GetTempPath(), $"mdfv-export-{Guid.NewGuid():N}");

    public MainViewModelExportTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md");
        _codec.SetFile(Guide, "# 안내");
        Directory.CreateDirectory(_outputFolder);
    }

    public void Dispose() => Directory.Delete(_outputFolder, recursive: true);

    [Fact]
    public async Task ExportCommands_WithoutDocument_CannotExecute()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act & Assert
        Assert.False(main.PrintCommand.CanExecute(null));
        Assert.False(main.ExportPdfCommand.CanExecute(null));
        Assert.False(main.ExportHtmlCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportCommands_WithDocumentShown_CanExecuteAndNotify()
    {
        // Arrange
        var main = await CreateMainAsync();
        var notified = 0;
        main.ExportPdfCommand.CanExecuteChanged += (_, _) => notified++;

        // Act
        await main.NavigateToFileAsync(Guide);

        // Assert
        Assert.True(main.PrintCommand.CanExecute(null));
        Assert.True(main.ExportPdfCommand.CanExecute(null));
        Assert.True(main.ExportHtmlCommand.CanExecute(null));
        Assert.True(notified > 0, "문서가 보이게 됐는데 명령 상태를 알리지 않았습니다.");
    }

    [Fact]
    public async Task PrintCommand_AsksViewerToShowPrintDialog()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();

        // Act
        await main.PrintCommand.ExecuteAsync();

        // Assert
        Assert.Equal(1, _viewer.PrintDialogCount);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task PrintCommand_ViewerFails_ShowsErrorInsteadOfThrowing()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        _viewer.OutputFailure = new InvalidOperationException("WebView2가 준비되지 않았습니다.");

        // Act
        await main.PrintCommand.ExecuteAsync();

        // Assert
        Assert.Equal((UiText.PrintFailedTitle, UiText.PrintFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.Contains(_logger.Entries, entry => entry.Level == "ERROR");
    }

    [Fact]
    public async Task ExportPdfCommand_SuggestsDocumentNameNextToSource()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();

        // Act — 취소한다
        await main.ExportPdfCommand.ExecuteAsync();

        // Assert
        var request = Assert.Single(_dialogs.SaveFileRequests);
        Assert.Equal(new SaveFileRequest(UiText.ExportPdfTitle, "guide.pdf", UiText.PdfFilterName, "pdf", Docs), request);
        Assert.Empty(_viewer.PdfPaths);
    }

    [Fact]
    public async Task ExportPdfCommand_PathPicked_SavesThroughViewerAndReports()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        var path = Path.Combine(_outputFolder, "out.pdf");
        _dialogs.SaveFileResult = path;

        // Act
        await main.ExportPdfCommand.ExecuteAsync();

        // Assert
        Assert.Equal([path], _viewer.PdfPaths);
        Assert.Equal(string.Format(UiText.ExportedFormat, "out.pdf"), main.StatusMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task ExportPdfCommand_ViewerReportsFailure_ShowsError()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        _dialogs.SaveFileResult = Path.Combine(_outputFolder, "out.pdf");
        _viewer.PdfResult = false;

        // Act
        await main.ExportPdfCommand.ExecuteAsync();

        // Assert
        Assert.Equal((UiText.ExportFailedTitle, UiText.ExportFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.Null(main.StatusMessage);
    }

    [Fact]
    public async Task ExportHtmlCommand_Cancelled_DoesNotAskViewer()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();

        // Act
        await main.ExportHtmlCommand.ExecuteAsync();

        // Assert
        var request = Assert.Single(_dialogs.SaveFileRequests);
        Assert.Equal(new SaveFileRequest(UiText.ExportHtmlTitle, "guide.html", UiText.HtmlFilterName, "html", Docs), request);
        Assert.Equal(0, _viewer.OutputHtmlCount);
    }

    [Fact]
    public async Task ExportHtmlCommand_PathPicked_WritesStandaloneFile()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        var path = Path.Combine(_outputFolder, "out.html");
        _dialogs.SaveFileResult = path;
        _viewer.OutputHtml = "<h1>안내</h1>";

        // Act
        await main.ExportHtmlCommand.ExecuteAsync();

        // Assert
        var bytes = await File.ReadAllBytesAsync(path);
        var html = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Contains("<title>guide</title>", html, StringComparison.Ordinal);
        Assert.Contains(Stylesheet, html, StringComparison.Ordinal);
        Assert.Contains("<h1>안내</h1>", html, StringComparison.Ordinal);
        Assert.Equal(string.Format(UiText.ExportedFormat, "out.html"), main.StatusMessage);
    }

    [Fact]
    public async Task ExportHtmlCommand_OverwritesExistingFileAndLeavesNoPartialFile()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        var path = Path.Combine(_outputFolder, "out.html");
        await File.WriteAllTextAsync(path, "예전 내용");
        _dialogs.SaveFileResult = path;
        _viewer.OutputHtml = "<p>새 내용</p>";

        // Act
        await main.ExportHtmlCommand.ExecuteAsync();

        // Assert
        Assert.Contains("<p>새 내용</p>", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        Assert.Equal(["out.html"], Directory.GetFiles(_outputFolder).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExportCommand_DocumentChangedWhileDialogOpen_DoesNotExport(bool isPdf)
    {
        // Arrange — 저장 위치를 묻는 동안 다른 문서가 열린다(다른 실행이 넘긴 파일 등)
        _treeService.SetFiles(Docs, "guide.md", "other.md");
        _codec.SetFile(@"C:\Docs\other.md", "# 다른 문서");
        var main = await CreateMainWithDocumentAsync();
        _dialogs.SaveFileResult = Path.Combine(_outputFolder, "out.bin");
        _dialogs.WhilePickingSaveFile = () => main.NavigateToFileAsync(@"C:\Docs\other.md").GetAwaiter().GetResult();

        // Act
        await (isPdf ? main.ExportPdfCommand : main.ExportHtmlCommand).ExecuteAsync();

        // Assert — 고른 이름으로 다른 문서가 나가지 않는다
        Assert.Empty(_viewer.PdfPaths);
        Assert.Equal(0, _viewer.OutputHtmlCount);
        Assert.Empty(Directory.GetFiles(_outputFolder));
        Assert.Equal(UiText.ExportDocumentChanged, main.StatusMessage);
    }

    [Fact]
    public async Task ExportHtmlCommand_ImageNotEmbedded_ReportsCount()
    {
        // Arrange
        var main = await CreateMainWithDocumentAsync();
        _dialogs.SaveFileResult = Path.Combine(_outputFolder, "out.html");
        _viewer.OutputHtml = "<img src=\"no-such-picture.png\">";

        // Act
        await main.ExportHtmlCommand.ExecuteAsync();

        // Assert
        Assert.Equal(string.Format(UiText.ExportedWithSkippedImagesFormat, "out.html", 1), main.StatusMessage);
    }

    [Fact]
    public async Task ExportHtmlCommand_CannotWriteFile_ShowsError()
    {
        // Arrange — 없는 폴더에 쓴다
        var main = await CreateMainWithDocumentAsync();
        _dialogs.SaveFileResult = Path.Combine(_outputFolder, "none", "out.html");

        // Act
        await main.ExportHtmlCommand.ExecuteAsync();

        // Assert
        Assert.Equal((UiText.ExportFailedTitle, UiText.ExportFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.Contains(_logger.Entries, entry => entry.Level == "ERROR");
    }

    private async Task<MainViewModel> CreateMainWithDocumentAsync()
    {
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        return main;
    }

    private async Task<MainViewModel> CreateMainAsync()
    {
        var main = NoSyncContext.Create(() =>
        {
            var tree = new FolderTreeViewModel(_treeService, _logger, new FavoritesList([]), NullFolderWatcher.Instance, _time);
            var document = new DocumentViewModel(
                _codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
            var services = new MainServices(_dialogs, new FakeShellLauncher(), _logger, _time)
            {
                FileExists = _codec.Exists,
                DirectoryExists = _treeService.FolderExists,
                ReadStylesheet = () => Stylesheet,
            };
            return new MainViewModel(tree, document, _viewer, services);
        });
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return main;
    }
}
