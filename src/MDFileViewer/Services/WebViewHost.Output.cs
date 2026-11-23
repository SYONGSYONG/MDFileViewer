using System.Runtime.InteropServices;
using System.Text.Json;
using MDFileViewer.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace MDFileViewer.Services;

/// <summary>
/// 인쇄, PDF·HTML 내보내기 (01 F-26, 02 §17.17). 결과물은 늘 밝은 테마다 — 색은 viewer.css의 인쇄용 규칙이 바꾸고, 어두운 테마로 그려 둔
/// 다이어그램은 내보내기 전에 viewer.js가 밝은 그림을 따로 준비한다(<see cref="PrepareOutputAsync"/>).
/// </summary>
public sealed partial class WebViewHost
{
    // 밝은 그림 준비가 끝났는지 다시 묻는 간격. 그림 하나를 그리는 데 걸리는 시간(수십 ms)과 비슷하게 둔다
    private static readonly TimeSpan OutputPollInterval = TimeSpan.FromMilliseconds(50);

    // PDF 만들기가 끝나지 않으면(렌더러가 멈춘 경우) 뷰어 잠금을 쥔 채 이후 모든 표시가 멈춘다. 쪽이 많은 문서도 넉넉히 끝나는 길이로 둔다
    private static readonly TimeSpan PdfTimeout = TimeSpan.FromMinutes(2);

    private const string ScriptTrue = "true";

    public Task ShowPrintDialogAsync(CancellationToken cancellationToken) =>
        RunOutputAsync(
            async core =>
            {
                await PrepareOutputAsync(core, cancellationToken);

                // 인쇄 창이 언제 닫히는지 알 수 없어 여기서 되돌리지 못한다 — 템플릿을 다시 불러올 때와 테마를 바꿀 때 되돌린다.
                // 그사이 화면은 템플릿이 전체를 칠하고 있어 달라 보이지 않는다
                _view.UsePaperBackground();

                // 브라우저의 인쇄 미리보기 창 — 프린터·용지·여백·"PDF로 저장"을 고를 수 있다. 창을 띄우고 바로 돌아온다
                core.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            },
            cancellationToken);

    public async Task<bool> SaveAsPdfAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var isSaved = false;
        await RunOutputAsync(
            async core =>
            {
                await PrepareOutputAsync(core, cancellationToken);
                var settings = core.Environment.CreatePrintSettings();

                // 코드 블록·표 머리글의 바탕색이 빠지지 않게 한다. 머리글·바닥글(날짜·주소)은 기본값대로 넣지 않는다
                settings.ShouldPrintBackgrounds = true;
                _view.UsePaperBackground();
                try
                {
                    isSaved = await core.PrintToPdfAsync(filePath, settings).WaitAsync(PdfTimeout);
                }
                finally
                {
                    _view.RestoreBackground();
                }
            },
            cancellationToken);
        return isSaved;
    }

    public async Task<string> GetOutputHtmlAsync(CancellationToken cancellationToken)
    {
        string? html = null;
        await RunOutputAsync(
            async core =>
            {
                await PrepareOutputAsync(core, cancellationToken);

                // 스크립트 결과는 JSON으로 온다(문자열이면 따옴표로 싸인 JSON 문자열)
                html = JsonSerializer.Deserialize<string>(await ExecuteScriptAsync(core, ViewerScriptBuilder.ExportHtml));
            },
            cancellationToken);
        return html ?? throw new InvalidOperationException("뷰어가 본문을 돌려주지 않았습니다.");
    }

    // 뷰어 프로세스가 끝났거나 답이 이상할 때의 예외를 호출자가 아는 한 종류로 바꾼다 — "내보내지 못했습니다"로 알릴 수 있게
    private async Task RunOutputAsync(Func<CoreWebView2, Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await RunInGateAsync(action, cancellationToken);
        }
        catch (Exception exception) when (exception is COMException or JsonException)
        {
            throw new InvalidOperationException("뷰어가 인쇄·내보내기 요청을 처리하지 못했습니다.", exception);
        }
    }

    // 잠금 안에서만 부른다. 스크립트의 Promise를 기다릴 통로가 없어(웹 메시지를 닫아 두었다) 끝났는지 물어 가며 기다린다
    private async Task PrepareOutputAsync(CoreWebView2 core, CancellationToken cancellationToken)
    {
        if (!_isTemplateLoaded || _displayed is null)
        {
            throw new InvalidOperationException("보여 준 문서가 없습니다.");
        }

        await ExecuteScriptAsync(core, ViewerScriptBuilder.PrepareOutput);
        var deadline = DateTime.UtcNow + ViewerResponseTimeout;
        while (await ExecuteScriptAsync(core, ViewerScriptBuilder.IsOutputReady) != ScriptTrue)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("내보낼 문서를 준비하는 데 너무 오래 걸립니다.");
            }

            await Task.Delay(OutputPollInterval, cancellationToken);
        }
    }
}
