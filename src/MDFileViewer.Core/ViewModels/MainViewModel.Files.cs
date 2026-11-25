using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 트리에서 하는 파일 관리 — 폴더에 새 문서, md 파일 이름 바꾸기·삭제 (01 F-27, 02 §17.18). 폴더 자체는 다루지 않는다.
/// </summary>
public sealed partial class MainViewModel
{
    private const string DefaultExtension = ".md";

    // "새 문서.md"가 있으면 "새 문서 2.md"…를 권한다. 이만큼 찾아도 빈 이름이 없으면 기본 이름을 그대로 권하고 대화상자가 겹친다고 알린다
    private const int MaxSuggestedNameNumber = 99;

    private readonly IFileOperations _files;

    /// <summary>폴더 "새 문서…" — 그 폴더에 빈 md 파일을 만들어 편집 모드로 연다.</summary>
    public RelayCommand<TreeNodeViewModel> NewDocumentCommand { get; private set; } = null!;

    /// <summary>md 파일 "이름 바꾸기…" (`F2`).</summary>
    public RelayCommand<TreeNodeViewModel> RenameFileCommand { get; private set; } = null!;

    /// <summary>md 파일 "삭제" (`Delete`) — 확인을 받은 뒤 휴지통으로 보낸다.</summary>
    public RelayCommand<TreeNodeViewModel> DeleteFileCommand { get; private set; } = null!;

    private void InitializeFileCommands()
    {
        NewDocumentCommand = new RelayCommand<TreeNodeViewModel>(node => Track(NewDocumentAsync(node)), node => node.IsFolderLike);
        RenameFileCommand = new RelayCommand<TreeNodeViewModel>(node => Track(RenameFileAsync(node)), node => node.IsMarkdownFile);
        DeleteFileCommand = new RelayCommand<TreeNodeViewModel>(node => Track(DeleteFileAsync(node)), node => node.IsMarkdownFile);
    }

    private async Task NewDocumentAsync(TreeNodeViewModel folder)
    {
        var folderPath = folder.FullPath;
        var suggested = SuggestNewDocumentName(folderPath);
        var request = new TextPromptRequest(
            UiText.NewDocumentTitle,
            UiText.FileNameLabel,
            suggested,
            Path.GetFileNameWithoutExtension(suggested).Length,
            UiText.CreateButton,
            input => DescribeNameProblem(input, DefaultExtension),
            input => DescribeTargetProblem(folderPath, input, DefaultExtension, currentPath: null));
        if (_dialogs.PromptText(request) is not { } answer)
        {
            return;
        }

        // 대화상자가 검사했지만 그것만 믿지 않는다 — 여기가 파일을 만드는 경계다
        if (ResolveTargetPath(folderPath, answer, DefaultExtension, currentPath: null, out var problem) is not { } path)
        {
            _dialogs.ShowError(UiText.FileOperationFailedTitle, problem ?? UiText.CreateFailedMessage);
            return;
        }

        if (!TryFileOperation(() => _files.CreateEmptyFile(path), "새 문서를 만들지 못했습니다.", UiText.CreateFailedMessage))
        {
            return;
        }

        await folder.ReloadChildrenAsync();
        await OpenInTabAsync(path, anchorId: null, isPreview: false, syncTree: true);

        // 빈 문서는 볼 것이 없다 — 바로 쓸 수 있게 편집 모드로 연다
        if (!Document.IsEditing)
        {
            BeginEditActive();
        }

        ShowStatus(string.Format(UiText.DocumentCreatedFormat, Path.GetFileName(path)));
    }

    private async Task RenameFileAsync(TreeNodeViewModel node)
    {
        var oldPath = node.FullPath;
        var tab = Tabs.Find(oldPath);
        if (tab is { IsDirty: true })
        {
            // 편집 중인 내용이 어느 파일의 것인지 흐려지지 않게, 저장한 뒤에 바꾸게 한다
            _dialogs.ShowInfo(UiText.RenameTitle, UiText.RenameUnsavedMessage);
            return;
        }

        if (Path.GetDirectoryName(oldPath) is not { } folderPath)
        {
            return;
        }

        var oldName = Path.GetFileName(oldPath);
        var extension = Path.GetExtension(oldPath);
        var request = new TextPromptRequest(
            UiText.RenameTitle,
            UiText.FileNameLabel,
            oldName,
            Path.GetFileNameWithoutExtension(oldName).Length,
            UiText.RenameButton,
            input => DescribeNameProblem(input, extension),
            input => DescribeTargetProblem(folderPath, input, extension, oldPath));
        if (_dialogs.PromptText(request) is not { } answer)
        {
            return;
        }

        if (ResolveTargetPath(folderPath, answer, extension, oldPath, out var problem) is not { } newPath)
        {
            _dialogs.ShowError(UiText.FileOperationFailedTitle, problem ?? UiText.RenameFailedMessage);
            return;
        }

        // 글자 하나 다르지 않으면 할 일이 없다(대소문자만 바꾼 것은 바꾼다)
        if (string.Equals(newPath, oldPath, StringComparison.Ordinal)
            || !TryFileOperation(() => _files.Rename(oldPath, newPath), "이름을 바꾸지 못했습니다.", UiText.RenameFailedMessage))
        {
            return;
        }

        await FollowRenamedFileAsync(node, tab, newPath);
        ShowStatus(string.Format(UiText.DocumentRenamedFormat, Path.GetFileName(newPath)));
    }

    // 이름을 바꾼 뒤 탭과 트리가 새 경로를 따라가게 한다
    private async Task FollowRenamedFileAsync(TreeNodeViewModel node, DocumentTabViewModel? tab, string newPath)
    {
        // 열려 있던 탭은 닫지 않고 새 경로를 가리키게 한다. 읽을 때 안 것(인코딩, 바깥 변경 확인의 기준)은 그대로 둔다 — 같은 파일이다
        if (tab is not null)
        {
            tab.Rename(newPath);

            // 보이는 문서였으면 트리보다 먼저 새 경로로 다시 보여 준다(제목·경로 표시가 따라온다). 바깥 변경 감시가 옛 이름을 보고 있는
            // 시간을 줄인다 — 그사이 알림이 오면 새 경로의 문서를 한 번 더 읽게 된다(리뷰 2026-11-24)
            if (ReferenceEquals(Tabs.ActiveTab, tab))
            {
                await Document.ShowTabAsync(tab, anchorId: null);
            }
        }

        if (node.Parent is { } parent)
        {
            await parent.ReloadChildrenAsync();
        }

        // 다시 읽으면 트리의 선택이 폴더로 옮겨 간다 — 바꾼 파일을 다시 고른다(문서를 새로 열지는 않는다)
        await SelectFileInTreeAsync(newPath);
    }

    private async Task DeleteFileAsync(TreeNodeViewModel node)
    {
        var path = node.FullPath;
        var name = Path.GetFileName(path);
        var tab = Tabs.Find(path);
        var toRecycleBin = _files.UsesRecycleBin(path);
        var message = string.Format(toRecycleBin ? UiText.RecycleConfirmFormat : UiText.DeletePermanentlyConfirmFormat, name);
        if (tab is { IsDirty: true })
        {
            message += Environment.NewLine + UiText.DeleteUnsavedWarning;
        }

        if (!_dialogs.Confirm(UiText.DeleteTitle, message, toRecycleBin ? UiText.RecycleButton : UiText.DeleteButton)
            || !TryFileOperation(() => DeleteUnlessGone(path, toRecycleBin), "문서를 지우지 못했습니다.", UiText.DeleteFailedMessage))
        {
            return;
        }

        // 지운 뒤에 탭을 닫는다 — 지우지 못했는데 탭부터 닫으면 저장하지 않은 변경만 잃는다. 저장할지는 묻지 않는다(위에서 알렸다)
        if (tab is not null)
        {
            await RemoveTabAsync(tab);
        }

        if (node.Parent is { } parent)
        {
            var wasSelected = ReferenceEquals(Tree.SelectedNode, node);
            await parent.ReloadChildrenAsync();
            if (wasSelected && Tabs.ActiveTab is null)
            {
                SelectWithoutOpening(parent);
            }
        }

        ShowStatus(string.Format(toRecycleBin ? UiText.DocumentRecycledFormat : UiText.DocumentDeletedFormat, name));
    }

    // 바깥에서 먼저 지워진 파일은 지우려던 결과와 같다 — 실패로 알리지 않고 탭·트리 정리로 넘어간다
    private void DeleteUnlessGone(string path, bool toRecycleBin)
    {
        try
        {
            _files.Delete(path, toRecycleBin);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.Info("지우려던 문서가 이미 없었습니다.");
        }
    }

    // 입력할 때마다 부른다 — 파일 시스템에 묻지 않는다(끊긴 네트워크 폴더에서 글자마다 창이 멈추지 않게, 리뷰 2026-11-24)
    private static string? DescribeNameProblem(string input, string fallbackExtension)
    {
        FileNameRules.TryNormalize(input, fallbackExtension, out var error);
        return error;
    }

    // 확인을 누를 때 부른다 — 이미 있는 이름인지까지 본다
    private string? DescribeTargetProblem(string folderPath, string input, string fallbackExtension, string? currentPath) =>
        ResolveTargetPath(folderPath, input, fallbackExtension, currentPath, out var error) is null ? error : null;

    /// <summary>
    /// 사용자가 적은 이름을 그 폴더 안의 경로로 바꾼다. 쓸 수 없는 이름이거나 이미 있는 이름이면 <c>null</c>과 이유.
    /// <paramref name="currentPath"/>는 이름을 바꾸는 파일 자신 — 대소문자만 바꾸는 것을 "이미 있음"으로 보지 않는다.
    /// </summary>
    private string? ResolveTargetPath(string folderPath, string input, string fallbackExtension, string? currentPath, out string? error)
    {
        if (FileNameRules.TryNormalize(input, fallbackExtension, out error) is not { } name)
        {
            return null;
        }

        var path = Path.Combine(folderPath, name);
        var isSameFile = currentPath is not null && string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase);
        if (!isSameFile && _files.Exists(path))
        {
            error = UiText.FileNameExists;
            return null;
        }

        return path;
    }

    private string SuggestNewDocumentName(string folderPath)
    {
        var first = UiText.NewDocumentDefaultName + DefaultExtension;
        if (!_files.Exists(Path.Combine(folderPath, first)))
        {
            return first;
        }

        for (var number = 2; number <= MaxSuggestedNameNumber; number++)
        {
            var candidate = $"{UiText.NewDocumentDefaultName} {number}{DefaultExtension}";
            if (!_files.Exists(Path.Combine(folderPath, candidate)))
            {
                return candidate;
            }
        }

        return first;
    }

    // 파일 작업의 실패(권한 없음, 다른 프로그램이 쓰는 중, 그사이 사라짐)는 로그와 대화상자로 알리고 끝낸다.
    // 예외의 메시지에는 파일 경로가 들어 있어 로그에는 예외의 종류만 남긴다 (02 §13)
    private bool TryFileOperation(Action operation, string logMessage, string userMessage)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _logger.Warn($"{logMessage} ({exception.GetType().Name})");
            _dialogs.ShowError(UiText.FileOperationFailedTitle, userMessage);
            return false;
        }
    }
}
