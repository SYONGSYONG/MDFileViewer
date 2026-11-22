using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>창 하나로 열기 E2E (01 F-23, 02 §17.14). 두 실행이 같은 "받는 창" 이름과 같은 설정 파일을 쓰게 해서 본다.</summary>
public sealed class SingleInstanceTests : IDisposable
{
    private const int ExitCodeSuccess = 0;

    private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(20);

    private readonly SampleDocuments _samples = new();
    private readonly string _settingsFolder = AppSession.NewSettingsFolder();
    private readonly Dictionary<string, string> _environment;

    public SingleInstanceTests()
    {
        _environment = new Dictionary<string, string>
        {
            [AppSession.SettingsVariableName] = SettingsPath,
            [AppSession.InstanceVariableName] = Guid.NewGuid().ToString("N"),
        };
    }

    private string SettingsPath => Path.Combine(_settingsFolder, "settings.json");

    public void Dispose()
    {
        _samples.Dispose();
        if (Directory.Exists(_settingsFolder))
        {
            Directory.Delete(_settingsFolder, recursive: true);
        }
    }

    [Fact]
    public void SecondLaunchWithFile_OpensTabInFirstWindowAndExits()
    {
        // Arrange — 기본 설정은 "창 하나로 열기"
        using var first = AppSession.Launch([_samples.PathOf(SampleDocuments.GuideFileName)], _environment);
        first.FindByName(SampleDocuments.GuideHeading);

        // Act
        using var second = AppSession.StartWithoutWindow([_samples.PathOf(SampleDocuments.OtherFileName)], _environment);

        // Assert — 두 번째 실행은 창 없이 끝나고, 첫 창에 탭이 하나 더 생겨 그 문서가 보인다
        try
        {
            Assert.True(second.WaitForExit(HandOverTimeout), "두 번째 실행이 요청을 넘기고 끝나지 않았습니다.");
            Assert.Equal(ExitCodeSuccess, second.ExitCode);
        }
        finally
        {
            KillIfRunning(second);
        }

        first.FindByName(SampleDocuments.OtherHeading);
        AppSession.WaitUntil(
            () => TabNames(first).SequenceEqual([SampleDocuments.GuideFileName, SampleDocuments.OtherFileName]), "넘겨받은 파일의 탭이 생기지 않았습니다.");
        Assert.Equal(string.Format(UiText.WindowTitleFormat, SampleDocuments.OtherFileName), first.MainWindow.Title);
    }

    [Fact]
    public void SecondLaunch_WhenSingleWindowTurnedOffInMenu_OpensItsOwnWindow()
    {
        // Arrange — `⋯` → "창 하나로 열기"를 끈다. 바로 저장되어 다음 실행부터 적용된다
        using var first = AppSession.Launch([_samples.PathOf(SampleDocuments.GuideFileName)], _environment);
        first.FindByName(SampleDocuments.GuideHeading);
        first.FindById("MoreButton").AsButton().Invoke();
        var item = FindMenuItem(first, UiText.SingleWindowMenu);
        Assert.Equal(ToggleState.On, item.Patterns.Toggle.Pattern.ToggleState.Value);
        item.AsMenuItem().Invoke();
        AppSession.WaitUntil(
            () => File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Contains("\"openInSingleWindow\": false", StringComparison.Ordinal),
            "설정이 바로 저장되지 않았습니다.");

        // Act — 창을 기다리는 실행: 넘기고 끝나면 창이 뜨지 않아 실패한다
        using var second = AppSession.Launch([_samples.PathOf(SampleDocuments.OtherFileName)], _environment);

        // Assert — 두 번째 창이 따로 뜨고 첫 창은 그대로다
        second.FindByName(SampleDocuments.OtherHeading);
        Assert.NotEqual(first.Application.ProcessId, second.Application.ProcessId);
        Assert.Equal([SampleDocuments.GuideFileName], TabNames(first));
    }

    private static List<string> TabNames(AppSession session) =>
        session.MainWindow.FindAllDescendants(cf => cf.ByAutomationId("DocumentTabName")).Select(element => element.Name).ToList();

    // 메뉴는 메인 창 밖의 팝업 창에 뜨므로 바탕 화면에서 이 앱 프로세스의 항목을 찾는다
    private static AutomationElement FindMenuItem(AppSession session, string name)
    {
        AutomationElement? item = null;
        AppSession.WaitUntil(
            () => (item = session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuItem)
                .And(cf.ByName(name)).And(cf.ByProcessId(session.Application.ProcessId)))) is not null,
            $"메뉴 항목이 없습니다: {name}");
        return item!;
    }

    private static void KillIfRunning(System.Diagnostics.Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
        }
    }
}
