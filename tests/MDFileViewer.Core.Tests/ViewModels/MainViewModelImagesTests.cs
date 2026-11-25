using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>그림 붙여넣기 — 저장 위치·이름·알림 (01 F-29, 02 §17.20). 클립보드와 끌기는 창의 일이라 여기서 보지 않는다.</summary>
public sealed class MainViewModelImagesTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Images = @"C:\Docs\images";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 25, 5, 30, 12, TimeSpan.Zero));
    private readonly FakeFileOperations _files;

    public MainViewModelImagesTests()
    {
        _files = new FakeFileOperations(_treeService, _codec);
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _files.AddFile(Guide, "# 안내");
    }

    [Fact]
    public async Task ImportPastedImage_SavesPngInImagesFolderAndReturnsLink()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        var expectedName = ImageLinks.PastedFileName(Guide, _time.GetLocalNow());

        // Act
        var links = await main.ImportPastedImageAsync(Png);

        // Assert — 문서 폴더의 images에 저장하고 상대 경로 링크를 돌려준다
        var written = Assert.Single(_files.Written);
        Assert.Equal(Path.Combine(Images, expectedName), written.Path);
        Assert.Equal(Png, written.Bytes);
        Assert.Equal([ImageLinks.BuildMarkdown(Guide, written.Path)], links);
        Assert.Equal(string.Format(UiText.ImageInsertedFormat, expectedName), main.StatusMessage);
    }

    [Fact]
    public async Task ImportPastedImage_NameTaken_SavesUnderNextNumber()
    {
        // Arrange — 같은 초에 한 번 더 붙였다
        var main = await CreateMainEditingGuideAsync();
        await main.ImportPastedImageAsync(Png);

        // Act
        await main.ImportPastedImageAsync(Png);

        // Assert — 덮어쓰지 않는다
        Assert.Equal(2, _files.Written.Count);
        Assert.EndsWith("-2.png", _files.Written[1].Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportPastedImage_NotEditing_DoesNothing()
    {
        // Arrange — 보기 모드
        var main = await CreateMainViewingGuideAsync();

        // Act
        var links = await main.ImportPastedImageAsync(Png);

        // Assert
        Assert.Empty(links);
        Assert.Empty(_files.Written);
    }

    [Fact]
    public async Task ImportPastedImage_CannotWrite_ReportsErrorAndReturnsNothing()
    {
        // Arrange — 읽기 전용 폴더
        var main = await CreateMainEditingGuideAsync();
        _files.NextFailure = new UnauthorizedAccessException(@"C:\Docs\images 접근 거부");

        // Act
        var links = await main.ImportPastedImageAsync(Png);

        // Assert — 알리고, 로그에는 예외 종류만 남긴다(경로 없음)
        Assert.Empty(links);
        Assert.Equal((UiText.FileOperationFailedTitle, UiText.ImageSaveFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.DoesNotContain(
            _logger.Entries, entry => entry.Exception is not null || entry.Message.Contains(@"C:\Docs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportImageFiles_CopiesOutsideFilesIntoImagesFolder()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();

        // Act
        var links = await main.ImportImageFilesAsync([@"D:\Pics\cat.png", @"D:\Pics\dog.jpg"]);

        // Assert — 원래 이름으로 복사하고 놓은 순서대로 링크를 돌려준다
        Assert.Equal(
            [(@"D:\Pics\cat.png", Path.Combine(Images, "cat.png")), (@"D:\Pics\dog.jpg", Path.Combine(Images, "dog.jpg"))],
            _files.Copied);
        Assert.Equal(["![cat](images/cat.png)", "![dog](images/dog.jpg)"], links);
        Assert.Equal(string.Format(UiText.ImagesInsertedFormat, 2), main.StatusMessage);
    }

    [Fact]
    public async Task ImportImageFiles_NameTaken_CopiesUnderNextNumber()
    {
        // Arrange — images에 다른 cat.png가 이미 있다
        _files.AddBinaryFile(Path.Combine(Images, "cat.png"));
        var main = await CreateMainEditingGuideAsync();

        // Act
        var links = await main.ImportImageFilesAsync([@"D:\Pics\cat.png"]);

        // Assert
        Assert.Equal([(@"D:\Pics\cat.png", Path.Combine(Images, "cat-2.png"))], _files.Copied);
        Assert.Equal(["![cat-2](images/cat-2.png)"], links);
    }

    [Fact]
    public async Task ImportImageFiles_AlreadyInsideDocumentFolder_LinksWithoutCopying()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();

        // Act
        var links = await main.ImportImageFilesAsync([@"C:\Docs\shots\a.png"]);

        // Assert — 이미 문서 옆에 있는 그림은 그 자리를 가리킨다
        Assert.Empty(_files.Copied);
        Assert.Equal(["![a](shots/a.png)"], links);
    }

    [Fact]
    public async Task ImportImageFiles_SkipsNonImagesAndSaysSo()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();

        // Act
        var links = await main.ImportImageFilesAsync([@"D:\Pics\cat.png", @"D:\Pics\notes.txt", @"D:\Pics"]);

        // Assert
        Assert.Single(links);
        Assert.Equal(
            string.Format(UiText.ImageInsertedFormat, "cat.png") + " " + string.Format(UiText.ImagesSkippedFormat, 2), main.StatusMessage);
    }

    [Fact]
    public async Task ImportImageFiles_NoImages_ReturnsNothingAndSaysSo()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();

        // Act
        var links = await main.ImportImageFilesAsync([@"D:\Pics\notes.txt"]);

        // Assert
        Assert.Empty(links);
        Assert.Empty(_files.Copied);
        Assert.Equal(UiText.NoImagesToInsert, main.StatusMessage);
    }

    [Fact]
    public async Task ImportImageFiles_MoreThanLimit_TakesFirstOnes()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        var paths = Enumerable.Range(1, AppConstants.MaxDroppedFiles + 3).Select(number => $@"D:\Pics\p{number}.png").ToList();

        // Act
        var links = await main.ImportImageFilesAsync(paths);

        // Assert — 실수로 폴더째 놓아도 문서가 넘치지 않는다
        Assert.Equal(AppConstants.MaxDroppedFiles, links.Count);
        Assert.Contains(string.Format(UiText.ImagesSkippedFormat, 3), main.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportImageFiles_SecondCopyFails_KeepsFirstAndReportsError()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        _files.FailAfterSuccesses(1, new IOException("디스크 가득 참"));

        // Act
        var links = await main.ImportImageFilesAsync([@"D:\Pics\cat.png", @"D:\Pics\dog.jpg"]);

        // Assert — 저장한 것까지는 넣는다
        Assert.Equal(["![cat](images/cat.png)"], links);
        Assert.Single(_dialogs.Errors);
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, new FavoritesList([]), NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(
            _codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
        var services = new MainServices(_dialogs, new FakeShellLauncher(), _logger, _time) { FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists, Files = _files };
        return new MainViewModel(tree, document, _viewer, services);
    });

    private async Task<MainViewModel> CreateMainViewingGuideAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFile(Guide), CancellationToken.None);
        return main;
    }

    private async Task<MainViewModel> CreateMainEditingGuideAsync()
    {
        var main = await CreateMainViewingGuideAsync();
        await main.ToggleEditCommand.ExecuteAsync();
        return main;
    }
}
