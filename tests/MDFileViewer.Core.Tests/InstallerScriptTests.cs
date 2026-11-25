namespace MDFileViewer.Core.Tests;

/// <summary>설치 스크립트와 앱이 같은 값을 쓰는지 (02 §15.1, 05 9-7).</summary>
public sealed class InstallerScriptTests
{
    [Fact]
    public void SetupScript_ChecksTheMutexTheAppCreates()
    {
        // Arrange
        var script = ReadScript();

        // Act
        var mutexLines = script.Where(line => line.StartsWith("AppMutex=", StringComparison.Ordinal)).ToArray();

        // Assert — 이름이 어긋나면 앱이 켜져 있어도 제거 프로그램이 알아채지 못해 잠긴 파일을 남긴다(2026-11-22 사용자 보고)
        Assert.Equal(["AppMutex=" + AppConstants.InstanceMutexName], mutexLines);
    }

    [Fact]
    public void SetupScript_OffersShellMenuTaskCheckedByDefault()
    {
        // Arrange
        var script = ReadScript();

        // Act
        var task = Assert.Single(script, line => line.StartsWith("Name: \"shellmenu\"", StringComparison.Ordinal));

        // Assert — 기본으로 켜 둔다(2026-11-24 결정). `unchecked`가 붙으면 설치만으로는 우클릭 메뉴가 생기지 않는다
        Assert.DoesNotContain("unchecked", task, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetupScript_RegistersShellMenuThroughTheAppOnlyWhenTaskIsChecked()
    {
        // Arrange
        var script = ReadScript();

        // Act
        var run = Assert.Single(script, line => line.Contains("--register", StringComparison.Ordinal) && line.StartsWith("Filename:", StringComparison.Ordinal));

        // Assert — 결과 창 없이(--silent), 끝날 때까지 기다리고, 체크했을 때만, 관리자 설치여도 설치하는 사람의 계정에 등록한다
        Assert.Contains("Parameters: \"--register --silent\"", run, StringComparison.Ordinal);
        Assert.Contains("Tasks: shellmenu", run, StringComparison.Ordinal);
        Assert.Contains("waituntilterminated", run, StringComparison.Ordinal);
        Assert.Contains("runasoriginaluser", run, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupScript_StaysAsciiAndKeepsKoreanTextInUtf8MessagesFile()
    {
        // Arrange
        var installer = Path.Combine(RepositoryPaths.Root(), "installer");
        var scriptBytes = File.ReadAllBytes(Path.Combine(installer, "setup.iss"));
        var messageBytes = File.ReadAllBytes(Path.Combine(installer, "Korean.custom.isl"));
        var messages = File.ReadAllText(Path.Combine(installer, "Korean.custom.isl"));

        // Assert — BOM 없는 스크립트는 ANSI로 읽혀 한글이 깨진다(04 §4). 메시지 파일은 BOM이 있어야 유니코드로 읽힌다
        Assert.All(scriptBytes, value => Assert.True(value < 0x80));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, messageBytes[..3]);
        Assert.Contains("ShellMenuTask=", messages, StringComparison.Ordinal);
        Assert.Contains("ShellMenuGroup=", messages, StringComparison.Ordinal);
    }

    [Fact]
    public void InstanceMutexName_HasNoCharactersInnoSetupTreatsSpecially()
    {
        // Assert — 쉼표는 여러 뮤텍스의 구분자이고, 스크립트는 ASCII만 쓴다(04 §4)
        Assert.DoesNotContain(',', AppConstants.InstanceMutexName);
        Assert.DoesNotContain('\\', AppConstants.InstanceMutexName);
        Assert.All(AppConstants.InstanceMutexName, character => Assert.True(char.IsAscii(character)));
    }

    private static string[] ReadScript() => File.ReadAllLines(Path.Combine(RepositoryPaths.Root(), "installer", "setup.iss"));
}
