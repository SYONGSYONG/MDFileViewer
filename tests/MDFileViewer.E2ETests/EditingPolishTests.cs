using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 줄 단위 스크롤 동기화·목차가 읽는 곳을 따라가기·표 도우미 E2E (01 F-36~F-38, 05 18-4). 칸 나누기·정리 규칙과 뷰모델의 경합은
/// Core 단위 테스트가 보고, 여기서는 뷰어 스크립트의 위치 계산과 실제 창의 키·버튼이 이어지는지만 본다.
/// </summary>
public sealed partial class EditingPolishTests : IDisposable
{
    private const string SyncFileName = "scroll-sync.md";
    private const string FollowFileName = "outline-follow.md";
    private const string TableFileName = "table.md";
    private const string NewTableFileName = "table-new.md";

    // 원본으로는 한 줄이지만 화면에서는 아주 높은 블록 — 비율로 맞추면 편집기가 한참 지난 뒤에도 미리보기는 이 블록 안이다 (01 F-36)
    private const int TallBlockHeight = 4000;
    private const int SyncParagraphs = 200;

    // 문단 k는 원본의 (FirstParagraphLine + 2k)번 줄이다(제목, 빈 줄, 높은 블록, 빈 줄 다음부터 한 줄 걸러 하나)
    private const int FirstParagraphLine = 5;
    private const int PageDownCount = 3;

    // 커서는 편집기 화면 안 어딘가에 있다(PageDown 뒤에는 아래쪽) — 맨 위 줄은 커서보다 많아야 한 쪽 앞이다.
    // 커서 줄의 문단에서 이만큼 앞까지의 문단이 미리보기에 보이면 따라온 것이다. 비율로 맞췄다면 높은 블록 안이라 문단이 하나도 보이지 않는다
    private const int ParagraphsPerEditorPage = 40;

    private const string FirstHeading = "따라가기 견본";
    private const string MiddleHeading = "가운데 절";
    private const string LastHeading = "맨 끝 절";
    private const string JumpLink = "맨 끝으로";
    private const int FillerParagraphs = 80;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void EditorScroll_PreviewFollowsByLine_PastTallBlock()
    {
        // Arrange
        var paragraphs = string.Concat(Enumerable.Range(0, SyncParagraphs).Select(number => $"{ParagraphName(number)}\n\n"));
        var path = _samples.PathOf(SyncFileName);
        File.WriteAllText(path, $"# 처음\n\n<div style=\"height:{TallBlockHeight}px\"></div>\n\n{paragraphs}");
        using var session = AppSession.Launch(path);
        EditorActions.WaitForTitle(session, SyncFileName);
        EditorActions.EnterEditMode(session);

        // Act — PageDown은 커서를 한 쪽씩 내리고 편집기는 커서가 보이게 따라 스크롤한다
        for (var press = 0; press < PageDownCount; press++)
        {
            Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
            Keyboard.Type(VirtualKeyShort.NEXT);
        }

        var caretLine = 0;
        var position = session.FindById("CursorPositionText");
        AppSession.WaitUntil(
            () => FirstNumber().Match(position.Name) is { Success: true } match && (caretLine = int.Parse(match.Value)) > FirstParagraphLine,
            $"PageDown을 눌렀는데 커서가 내려가지 않았습니다. 지금: '{position.Name}'");

        // Assert — 편집기에 보이는 줄들의 문단이 미리보기에도 보인다
        var last = (caretLine - FirstParagraphLine + 1) / 2;
        var first = Math.Max(0, last - ParagraphsPerEditorPage);
        var candidates = Enumerable.Range(first, last - first + 1).Select(ParagraphName).ToList();
        AppSession.WaitUntil(
            () => candidates.Any(name => IsShownInWindow(session, name)),
            $"편집기를 {caretLine}번 줄까지 내렸는데 미리보기에 그 근처 문단({candidates[0]}~{candidates[^1]})이 보이지 않습니다.");
    }

    [Fact]
    public void Outline_SelectsHeadingBeingRead_WhenDocumentScrolls()
    {
        // Arrange — 맨 끝 제목으로 가는 문서 안 링크로 스크롤한다(목차를 누르지 않는다)
        var filler = string.Concat(Enumerable.Range(0, FillerParagraphs).Select(number => $"채움 문단 {number}\n\n"));
        var path = _samples.PathOf(FollowFileName);
        File.WriteAllText(
            path,
            $"# {FirstHeading}\n\n[{JumpLink}](#{LastHeading.Replace(' ', '-')})\n\n## {MiddleHeading}\n\n{filler}## {LastHeading}\n\n끝\n");
        using var session = AppSession.Launch(path);
        session.FindByName(FirstHeading);
        session.FindById("OutlineButton").AsButton().Invoke();
        var list = session.FindById("OutlineList");
        AppSession.WaitUntil(() => session.FindSelectables(list).Count == 3, "목차에 제목 세 개가 보이지 않습니다.");

        // 맨 위에서는 첫 제목을 읽는 중이다
        AppSession.WaitUntil(() => IsSelected(session, list, FirstHeading), "문서 맨 위인데 목차의 첫 제목이 표시되지 않았습니다.");

        // Act
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        var link = SafeInput.CenterOf(session.FindLink(JumpLink).BoundingRectangle);
        SafeInput.EnsureCanSendAt(session, link);
        Mouse.Click(link);

        // Assert — 목차의 표시가 맨 끝 제목으로 옮겨 가고, 문서는 그곳에 머문다
        AppSession.WaitUntil(() => IsSelected(session, list, LastHeading), "문서를 맨 끝으로 스크롤했는데 목차의 표시가 따라오지 않았습니다.");
        Assert.True(IsShownInWindow(session, "끝"), "목차의 표시가 바뀌면서 문서가 다른 곳으로 스크롤했습니다.");
    }

    [Fact]
    public void Tab_InTable_MovesBetweenCellsAlignsAndAppendsRow()
    {
        // Arrange — 커서는 문서 처음(줄을 여는 `|` 앞)에 있다
        var path = _samples.PathOf(TableFileName);
        File.WriteAllText(path, "| a | b |\n|---|---|\n| 1 | 2 |");
        using var session = AppSession.Launch(path);
        EditorActions.WaitForTitle(session, TableFileName);
        EditorActions.EnterEditMode(session);

        // Act — 첫 칸을 골라 고쳐 쓰고, 마지막 칸을 지나 새 줄의 첫 칸에 쓴다
        TypeKey(session, VirtualKeyShort.TAB);
        TypeText(session, "x");
        for (var press = 0; press < 4; press++)
        {
            TypeKey(session, VirtualKeyShort.TAB);
        }

        TypeText(session, "z");

        // Shift+Tab은 이전 칸(앞 줄의 마지막 칸)의 글을 고른다
        EditorActions.Press(session, VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
        TypeText(session, "9");
        EditorActions.WaitForUnsavedMark(session, TableFileName);
        EditorActions.Save(session, TableFileName);

        // Assert — 마지막 Shift+Tab이 표를 다시 가지런히 했다
        Assert.Equal(["| x   | b   |", "| --- | --- |", "| 1   | 9   |", "| z   |     |"], File.ReadAllLines(path));
    }

    [Fact]
    public void TableButton_InsertsTableBelowCurrentLine_WithFirstHeaderSelected()
    {
        // Arrange
        var path = _samples.PathOf(NewTableFileName);
        File.WriteAllText(path, "글");
        using var session = AppSession.Launch(path);
        EditorActions.WaitForTitle(session, NewTableFileName);
        var editor = EditorActions.EnterEditMode(session);

        // Act — 버튼을 누른 뒤 포커스가 편집기로 돌아와 고른 제목 칸을 바로 고쳐 쓸 수 있다
        session.FindById("FormatTableButton").AsButton().Invoke();
        AppSession.WaitUntil(() => EditorActions.IsFocusWithin(session, editor), "표를 넣은 뒤 편집기가 포커스를 받지 않았습니다.");
        TypeText(session, "H");
        EditorActions.WaitForUnsavedMark(session, NewTableFileName);
        EditorActions.Save(session, NewTableFileName);

        // Assert
        var second = string.Format(UiText.TableHeaderFormat, 2);
        var third = string.Format(UiText.TableHeaderFormat, 3);
        Assert.Equal(
            ["글", string.Empty, $"| H | {second} | {third} |", "| ------ | ------ | ------ |", "|        |        |        |"],
            File.ReadAllLines(path));
    }

    private static string ParagraphName(int number) => $"문단 {number:D3}";

    private static void TypeKey(AppSession session, VirtualKeyShort key)
    {
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(key);
    }

    private static void TypeText(AppSession session, string text)
    {
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(text);
    }

    private static bool IsSelected(AppSession session, AutomationElement list, string name) =>
        session.FindSelectables(list).FirstOrDefault(item => item.Name == name) is { } item
        && item.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;

    // 문서 안의 요소(목차 항목이 아닌 것)가 창 안에 보이는지
    private static bool IsShownInWindow(AppSession session, string name) =>
        session.MainWindow.FindAllDescendants(cf => cf.ByName(name))
            .Any(element => !element.Patterns.SelectionItem.IsSupported
                && !element.Properties.IsOffscreen.ValueOrDefault
                && session.MainWindow.BoundingRectangle.Contains(SafeInput.CenterOf(element.BoundingRectangle)));

    [GeneratedRegex("[0-9]+")]
    private static partial Regex FirstNumber();
}
