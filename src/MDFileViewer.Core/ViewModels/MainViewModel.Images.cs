using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 그림 붙여넣기 (01 F-29, 02 §17.20) — 붙이거나 놓은 그림을 문서 폴더의 <c>images</c>에 저장하고, 편집기에 넣을 Markdown을 돌려준다.
/// 클립보드와 끌기를 읽고 편집기에 글을 넣는 일은 창이 한다.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// 클립보드에서 온 그림(PNG 바이트)을 저장한다. 넣을 Markdown 한 조각을 돌려주며, 편집 중이 아니거나 저장하지 못했으면 비어 있다.
    /// </summary>
    public async Task<IReadOnlyList<string>> ImportPastedImageAsync(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (EditingDocumentPath() is not { } documentPath)
        {
            return [];
        }

        var fileName = ImageLinks.PastedFileName(documentPath, _timeProvider.GetLocalNow());
        if (await SaveImageAsync(documentPath, fileName, target => _files.WriteFile(target, pngBytes)) is not { } savedPath)
        {
            return [];
        }

        ShowStatus(string.Format(UiText.ImageInsertedFormat, Path.GetFileName(savedPath)));
        return [ImageLinks.BuildMarkdown(documentPath, savedPath)];
    }

    /// <summary>
    /// 복사하거나 끌어 온 파일들 가운데 그림을 문서 폴더로 가져온다. 넣을 Markdown을 준 순서대로 돌려준다. 이미 문서 폴더 안에 있는 그림은
    /// 복사하지 않고 그 자리를 가리킨다. 그림이 아닌 항목과 <see cref="AppConstants.MaxDroppedFiles"/>개를 넘은 것은 건너뛰고 알린다.
    /// </summary>
    public async Task<IReadOnlyList<string>> ImportImageFilesAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (EditingDocumentPath() is not { } documentPath)
        {
            return [];
        }

        var images = paths.Where(ImageLinks.IsImageFile).Take(AppConstants.MaxDroppedFiles).ToList();
        if (images.Count == 0)
        {
            ShowStatus(UiText.NoImagesToInsert);
            return [];
        }

        var links = new List<string>();
        string? lastName = null;
        foreach (var source in images)
        {
            // 이미 문서 옆에 있는 그림은 그 자리를 가리킨다 — 같은 그림을 images에 한 벌 더 만들지 않는다
            var imagePath = ImageLinks.RelativePath(documentPath, source) is not null
                ? source
                : await SaveImageAsync(documentPath, Path.GetFileName(source), target => _files.CopyFile(source, target));
            if (imagePath is null)
            {
                // 알림은 이미 띄웠다. 같은 까닭으로 나머지도 실패할 것이므로 멈추고, 저장한 것까지만 넣는다
                break;
            }

            links.Add(ImageLinks.BuildMarkdown(documentPath, imagePath));
            lastName = Path.GetFileName(imagePath);
        }

        if (links.Count > 0)
        {
            var inserted = links.Count == 1 ? string.Format(UiText.ImageInsertedFormat, lastName) : string.Format(UiText.ImagesInsertedFormat, links.Count);
            var skipped = paths.Count - images.Count;
            ShowStatus(skipped > 0 ? $"{inserted} {string.Format(UiText.ImagesSkippedFormat, skipped)}" : inserted);
        }

        return links;
    }

    // 그림은 편집 중인 문서에만 넣는다 — 보기 모드에는 넣을 편집기가 없다
    private string? EditingDocumentPath() => Document.IsEditing ? Tabs.ActiveTab?.FilePath : null;

    /// <summary>
    /// 문서 폴더의 <c>images</c>에 겹치지 않는 이름으로 저장하고 그 경로를 돌려준다. 실패하면 알리고 <c>null</c>.
    /// 큰 그림이나 네트워크 폴더에서 화면이 멈추지 않게 UI 스레드 밖에서 한다 (02 §9).
    /// </summary>
    private async Task<string?> SaveImageAsync(string documentPath, string fileName, Action<string> save)
    {
        if (Path.GetDirectoryName(documentPath) is not { } documentFolder)
        {
            return null;
        }

        var imagesFolder = Path.Combine(documentFolder, AppConstants.ImagesFolderName);
        try
        {
            return await Task.Run(() =>
            {
                var name = ImageLinks.UniqueName(fileName, candidate => _files.Exists(Path.Combine(imagesFolder, candidate)))
                    ?? throw new IOException("겹치지 않는 그림 이름을 찾지 못했습니다.");
                var target = Path.Combine(imagesFolder, name);
                save(target);
                return target;
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // 예외의 메시지에는 파일 경로가 들어 있어 로그에는 예외의 종류만 남긴다 (02 §13)
            _logger.Warn($"그림을 저장하지 못했습니다. ({exception.GetType().Name})");
            _dialogs.ShowError(UiText.FileOperationFailedTitle, UiText.ImageSaveFailedMessage);
            return null;
        }
    }
}
