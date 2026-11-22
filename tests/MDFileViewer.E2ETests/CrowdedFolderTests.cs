using System.Globalization;
using FlaUI.Core.AutomationElements;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 형제가 많은 폴더 안쪽의 파일을 열었을 때 고른 행이 화면에 보이는지 확인한다 (01 F-01·F-02·F-18).
/// E2E 샘플을 `%TEMP%`(바로 아래 항목 1,000개 이상) 아래에 두었을 때 고른 행이 화면 밖에 남는 실패가 났다(2026-11-22, 05 E2E 회귀 기록) —
/// 그 조건을 `%TEMP%` 없이 다시 만든다.
/// </summary>
public sealed class CrowdedFolderTests : IDisposable
{
    // 샘플 폴더(MDFileViewer.E2E)가 이름순으로 형제들 한가운데에 오게 앞뒤로 나눠 만든다 — `%TEMP%`와 비슷한 규모
    private const int SiblingsBeforeSample = 700;
    private const int SiblingsAfterSample = 700;
    private const string BeforePrefix = "a-";
    private const string AfterPrefix = "z-";

    // 폴더가 바뀐 뒤 자동 새로고침(0.5초 뒤 다시 읽기)이 끝나고 화면이 자리 잡을 시간. 새 형제는 화면 밖이라 그 행으로는 기다릴 수 없다.
    private static readonly TimeSpan AutoRefreshSettleTime = TimeSpan.FromSeconds(2);
    private const int ChurnRounds = 5;

    private readonly string _crowdedFolder;
    private readonly SampleDocuments _samples;

    public CrowdedFolderTests()
    {
        _crowdedFolder = Path.Combine(SampleDocuments.DefaultBaseDirectory(), "crowded-" + Guid.NewGuid().ToString("N"));
        for (var number = 0; number < SiblingsBeforeSample; number++)
        {
            Directory.CreateDirectory(Path.Combine(_crowdedFolder, SiblingName(BeforePrefix, number)));
        }

        for (var number = 0; number < SiblingsAfterSample; number++)
        {
            Directory.CreateDirectory(Path.Combine(_crowdedFolder, SiblingName(AfterPrefix, number)));
        }

        _samples = new SampleDocuments(_crowdedFolder);
    }

    public void Dispose()
    {
        _samples.Dispose();
        if (Directory.Exists(_crowdedFolder))
        {
            Directory.Delete(_crowdedFolder, recursive: true);
        }
    }

    [Fact]
    public void Launch_WithFileInsideCrowdedFolder_ShowsSelectedRow()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));

        // Assert
        session.FindByName(SampleDocuments.GuideHeading);
        AssertSelectedRowIsVisible(session, "시작 직후");
    }

    [Fact]
    public void SiblingsChangeInCrowdedFolder_SelectedRowStaysVisible()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        AssertSelectedRowIsVisible(session, "시작 직후");

        for (var round = 1; round <= ChurnRounds; round++)
        {
            // Act — 고른 행보다 위쪽에 형제 폴더가 생겼다가 없어진다(임시 폴더에서 다른 프로그램이 하는 일)
            var added = Path.Combine(_crowdedFolder, BeforePrefix + "new-" + round.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(added);
            Thread.Sleep(AutoRefreshSettleTime);

            // Assert
            AssertSelectedRowIsVisible(session, $"{round}번째 형제 폴더가 생긴 뒤");

            Directory.Delete(added);
            Thread.Sleep(AutoRefreshSettleTime);
            AssertSelectedRowIsVisible(session, $"{round}번째 형제 폴더가 없어진 뒤");
        }
    }

    private static void AssertSelectedRowIsVisible(AppSession session, string when)
    {
        var item = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.GuideFileName);
        AppSession.WaitUntil(() => IsSelected(item), $"{when}: 트리에서 고른 파일이 선택되어 있지 않습니다.");
        Assert.False(item.IsOffscreen, $"{when}: 고른 행이 화면 밖입니다.");
    }

    private static bool IsSelected(AutomationElement element) => element.Patterns.SelectionItem.Pattern.IsSelected.Value;

    private static string SiblingName(string prefix, int number) => prefix + number.ToString("D4", CultureInfo.InvariantCulture);
}
