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
}
