using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MDFileViewer.Controls;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Views;

/// <summary>
/// 앱에 내장한 문서를 보여 주는 창 — Markdown 문법 도움말(01 F-12, 02 §17.4)과 릴리즈 노트(01 F-24, 02 §17.15). 문서는 출력 폴더의
/// <c>Resources\</c> 아래 md 파일이고 자기 뷰어(WebViewHost)로 보여 준다. 왼쪽 목록(<c>## </c> 제목)을 고르면 그 제목으로 스크롤하고,
/// 문서 안 링크는 앵커만 따라가며 외부 링크는 브라우저로 연다.
/// </summary>
public partial class HelpWindow : Window
{
    private readonly IViewerHost _viewer;
    private readonly IShellLauncher _shell;
    private readonly IAppLogger _logger;
    private readonly string _helpPath;

    /// <param name="automationId">창의 AutomationId — 같은 창 종류를 문서마다 구분한다.</param>
    /// <param name="contentsName">왼쪽 목록의 접근성 이름("목차", "버전").</param>
    public HelpWindow(
        Func<MarkdownView, IViewerHost> createViewer, IShellLauncher shell, IAppLogger logger, string helpPath, string title, string automationId, string contentsName)
    {
        InitializeComponent();
        Title = title;
        AutomationProperties.SetAutomationId(this, automationId);
        AutomationProperties.SetName(Contents, contentsName);
        _viewer = createViewer(HelpViewer);
        _shell = shell;
        _logger = logger;
        _helpPath = helpPath;
        _viewer.LinkActivated += OnLinkActivated;
    }

    public IViewerHost Viewer => _viewer;

    /// <summary>도움말을 읽어 목차와 문서를 채운다. 뷰어 초기화가 끝난 뒤에 부른다.</summary>
    public async Task ShowHelpAsync(string version)
    {
        var help = HelpDocument.Create(await File.ReadAllTextAsync(_helpPath), version);
        Contents.ItemsSource = help.Sections;
        var bodyHtml = await Task.Run(() => MarkdownRenderer.RenderBody(help.Markdown));
        await _viewer.ShowDocumentAsync(bodyHtml, _helpPath, CancellationToken.None);
    }

    private async void OnSectionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Contents.SelectedItem is not HelpSection section)
        {
            return;
        }

        try
        {
            await _viewer.ScrollToAnchorAsync(section.AnchorId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // 스크롤만 못 한 것이라 창은 계속 쓴다
            _logger.Warn("도움말 목차로 이동하지 못했습니다.", exception);
        }
    }

    private async void OnLinkActivated(object? sender, LinkActivatedEventArgs e)
    {
        try
        {
            switch (await Task.Run(() => LinkClassifier.Classify(e.Url, _helpPath)))
            {
                case LinkTarget.External external:
                    _shell.OpenExternal(external.Uri);
                    break;
                case LinkTarget.CurrentDocumentAnchor anchor:
                    await _viewer.ScrollToAnchorAsync(anchor.AnchorId, CancellationToken.None);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logger.Warn("도움말 링크를 열지 못했습니다.", exception);
        }
    }
}
