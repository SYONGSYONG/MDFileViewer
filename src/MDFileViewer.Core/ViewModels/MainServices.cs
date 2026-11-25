using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// <see cref="MainViewModel"/>이 쓰는 바깥 서비스 묶음. 생성자 인수가 길어지지 않게 모았다. 감시기와 파일 확인은 테스트·감시 없는 실행을 위해
/// 기본값이 있다.
/// </summary>
public sealed record MainServices(IDialogService Dialogs, IShellLauncher Shell, IAppLogger Logger, TimeProvider Time)
{
    /// <summary>활성 탭 파일의 바깥 변경 감시 (01 F-18).</summary>
    public IFolderWatcher Watcher { get; init; } = NullFolderWatcher.Instance;

    /// <summary>탭 복원 때 파일이 아직 있는지 (01 F-17). 테스트는 가짜 파일 목록으로 바꾼다.</summary>
    public Func<string, bool> FileExists { get; init; } = File.Exists;

    /// <summary>끌어다 놓은 항목이 폴더인지 (01 F-19). 테스트는 가짜 폴더 목록으로 바꾼다.</summary>
    public Func<string, bool> DirectoryExists { get; init; } = Directory.Exists;

    /// <summary>HTML로 내보낼 때 파일 안에 넣을 뷰어 스타일 (01 F-26). 기본은 출력 폴더의 <c>Resources\viewer.css</c>다.</summary>
    public Func<string> ReadStylesheet { get; init; } = () => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName, AppConstants.ViewerStylesheetFileName));

    /// <summary>빠른 열기·폴더 안 내용 검색이 훑을 md 파일 (01 F-30·F-31). 테스트는 가짜 목록으로 바꾼다.</summary>
    public IMarkdownFileFinder FileFinder { get; init; } = new MarkdownFileFinder();

    /// <summary>최근에 본 문서 (01 F-34). 구성 루트가 설정에서 읽은 목록을 넣는다.</summary>
    public RecentList RecentDocuments { get; init; } = new(AppConstants.RecentDocumentsCapacity);

    /// <summary>최근에 일부러 연 폴더 (01 F-34).</summary>
    public RecentList RecentFolders { get; init; } = new(AppConstants.RecentFoldersCapacity);

    /// <summary>폴더 안 내용 검색 (01 F-31). 테스트는 가짜 파일로 바꾼다.</summary>
    public IFolderSearch FolderSearch { get; init; } = new FolderSearch(new MarkdownFileFinder(), new TextFileCodec());

    /// <summary>트리에서 하는 새 문서·이름 바꾸기·삭제 (01 F-27). 테스트는 가짜 파일 시스템으로 바꾼다.</summary>
    public IFileOperations Files { get; init; } = new FileOperations();
}
