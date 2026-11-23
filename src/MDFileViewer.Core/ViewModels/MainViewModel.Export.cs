using System.Text;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 인쇄, PDF·HTML로 내보내기 (01 F-26, 02 §17.17). 뷰어에 지금 보이는 문서를 내보낸다 — 편집 모드에서는 미리보기에 보이는 내용이다.
/// 결과물은 앱 테마와 상관없이 밝은 테마다.
/// </summary>
public sealed partial class MainViewModel
{
    private const string PdfExtension = "pdf";
    private const string HtmlExtension = "html";

    // 쓰는 중인 파일의 이름 끝. 다 쓴 뒤에 고른 이름으로 바꾼다
    private const string PartialFileSuffix = ".mdfv-part";

    // BOM 없는 UTF-8 — 내보낸 파일은 <meta charset>으로 인코딩을 밝힌다
    private static readonly UTF8Encoding ExportEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IViewerHost _viewer;
    private readonly Func<string> _readStylesheet;

    /// <summary>인쇄 (`Ctrl+P`) — 인쇄 미리보기 창을 연다.</summary>
    public AsyncRelayCommand PrintCommand { get; private set; } = null!;

    public AsyncRelayCommand ExportPdfCommand { get; private set; } = null!;

    public AsyncRelayCommand ExportHtmlCommand { get; private set; } = null!;

    private void InitializeExportCommands()
    {
        PrintCommand = new AsyncRelayCommand(PrintAsync, OnCommandFailed, CanExport);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, OnCommandFailed, CanExport);
        ExportHtmlCommand = new AsyncRelayCommand(ExportHtmlAsync, OnCommandFailed, CanExport);
    }

    private void NotifyExportCommands()
    {
        PrintCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportHtmlCommand.NotifyCanExecuteChanged();
    }

    private bool CanExport() => Document.IsDocumentVisible && Document.FilePath is not null;

    private async Task PrintAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _viewer.ShowPrintDialogAsync(cancellationToken);
        }
        catch (Exception exception) when (IsExportFailure(exception))
        {
            _logger.Error("인쇄 창을 열지 못했습니다.", exception);
            _dialogs.ShowError(UiText.PrintFailedTitle, UiText.PrintFailedMessage);
        }
    }

    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (PickExportTarget(UiText.ExportPdfTitle, UiText.PdfFilterName, PdfExtension) is not var (_, path))
        {
            return;
        }

        try
        {
            if (!await _viewer.SaveAsPdfAsync(path, cancellationToken))
            {
                _logger.Warn("뷰어가 PDF를 만들지 못했습니다.");
                _dialogs.ShowError(UiText.ExportFailedTitle, UiText.ExportFailedMessage);
                return;
            }

            ShowStatus(string.Format(UiText.ExportedFormat, Path.GetFileName(path)));
        }
        catch (Exception exception) when (IsExportFailure(exception))
        {
            _logger.Error("PDF로 내보내지 못했습니다.", exception);
            _dialogs.ShowError(UiText.ExportFailedTitle, UiText.ExportFailedMessage);
        }
    }

    private async Task ExportHtmlAsync(CancellationToken cancellationToken)
    {
        if (PickExportTarget(UiText.ExportHtmlTitle, UiText.HtmlFilterName, HtmlExtension) is not var (documentPath, path))
        {
            return;
        }

        try
        {
            var bodyHtml = await _viewer.GetOutputHtmlAsync(cancellationToken);
            var title = Path.GetFileNameWithoutExtension(documentPath);

            // 그림 읽기와 base64 변환, 파일 쓰기는 UI 스레드 밖에서 한다
            var result = await Task.Run(
                async () =>
                {
                    var built = HtmlExporter.Build(
                        title,
                        bodyHtml,
                        _readStylesheet(),
                        documentPath,
                        imagePath => HtmlExporter.ReadImageFile(imagePath, AppConstants.MaxEmbeddedImageBytes));
                    await WriteThroughPartialFileAsync(path, built.Html, cancellationToken);
                    return built;
                },
                cancellationToken);
            var fileName = Path.GetFileName(path);
            ShowStatus(result.SkippedImageCount > 0
                ? string.Format(UiText.ExportedWithSkippedImagesFormat, fileName, result.SkippedImageCount)
                : string.Format(UiText.ExportedFormat, fileName));
        }
        catch (Exception exception) when (IsExportFailure(exception))
        {
            _logger.Error("HTML로 내보내지 못했습니다.", exception);
            _dialogs.ShowError(UiText.ExportFailedTitle, UiText.ExportFailedMessage);
        }
    }

    /// <summary>
    /// 저장 위치를 묻는다. 원본과 같은 폴더·같은 이름(확장자만 다름)을 먼저 권한다. 취소했거나, 대화상자가 떠 있는 동안 보고 있는 문서가
    /// 바뀌었으면(다른 실행이 넘긴 파일이 열린 경우 등) <c>null</c> — 고른 이름으로 다른 문서가 나가지 않게 한다(리뷰 2026-11-23).
    /// </summary>
    private (string DocumentPath, string OutputPath)? PickExportTarget(string title, string filterName, string extension)
    {
        if (Document.FilePath is not { } documentPath)
        {
            return null;
        }

        var suggested = $"{Path.GetFileNameWithoutExtension(documentPath)}.{extension}";
        var request = new SaveFileRequest(title, suggested, filterName, extension, Path.GetDirectoryName(documentPath));
        if (_dialogs.PickSaveFile(request) is not { } outputPath)
        {
            return null;
        }

        if (!CanExport() || !string.Equals(Document.FilePath, documentPath, StringComparison.OrdinalIgnoreCase))
        {
            ShowStatus(UiText.ExportDocumentChanged);
            return null;
        }

        return (documentPath, outputPath);
    }

    // 덮어쓰기로 고른 기존 파일을 쓰다 실패해(디스크 가득 참 등) 반쪽으로 남기지 않게, 옆에 다 쓴 뒤에 바꾼다
    private static async Task WriteThroughPartialFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        var partialPath = path + PartialFileSuffix;
        try
        {
            await File.WriteAllTextAsync(partialPath, content, ExportEncoding, cancellationToken);
            File.Move(partialPath, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(partialPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 지우지 못해도 원래의 실패를 알리는 것이 먼저다
            }

            throw;
        }
    }

    // 파일 쓰기 실패와 뷰어 실패(준비 안 됨·응답 없음)는 사용자에게 알리고 끝낸다. 그 밖의 예외는 명령의 공통 처리로 간다
    private static bool IsExportFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or NotSupportedException
            or UriFormatException;
}
