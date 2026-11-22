namespace MDFileViewer.Core.Tests;

/// <summary>설치 스크립트와 앱이 같은 값을 쓰는지 (02 §15.1, 05 9-7).</summary>
public sealed class InstallerScriptTests
{
    [Fact]
    public void SetupScript_ChecksTheMutexTheAppCreates()
    {
        // Arrange
        var script = File.ReadAllLines(Path.Combine(RepositoryPaths.Root(), "installer", "setup.iss"));

        // Act
        var mutexLines = script.Where(line => line.StartsWith("AppMutex=", StringComparison.Ordinal)).ToArray();

        // Assert — 이름이 어긋나면 앱이 켜져 있어도 제거 프로그램이 알아채지 못해 잠긴 파일을 남긴다(2026-11-22 사용자 보고)
        Assert.Equal(["AppMutex=" + AppConstants.InstanceMutexName], mutexLines);
    }

    [Fact]
    public void InstanceMutexName_HasNoCharactersInnoSetupTreatsSpecially()
    {
        // Assert — 쉼표는 여러 뮤텍스의 구분자이고, 스크립트는 ASCII만 쓴다(04 §4)
        Assert.DoesNotContain(',', AppConstants.InstanceMutexName);
        Assert.DoesNotContain('\\', AppConstants.InstanceMutexName);
        Assert.All(AppConstants.InstanceMutexName, character => Assert.True(char.IsAscii(character)));
    }
}
