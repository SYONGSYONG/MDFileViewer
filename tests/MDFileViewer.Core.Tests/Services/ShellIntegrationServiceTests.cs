using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using Microsoft.Win32;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// 실제 레지스트리에 쓰는 통합 테스트 (02 §8.4). 루트는 <c>HKCU\Software\MDFileViewer.Tests\&lt;GUID&gt;\Classes</c>이고 끝나면 지운다 —
/// 실제 <c>Software\Classes</c>는 건드리지 않는다.
/// </summary>
public sealed class ShellIntegrationServiceTests : IDisposable
{
    private const string TestsRoot = @"Software\MDFileViewer.Tests";
    private const string ExePath = @"C:\Apps\MDFileViewer\MDFileViewer.exe";

    private readonly string _testKey = TestsRoot + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryAccess _registry;
    private readonly ShellIntegrationService _service;

    public ShellIntegrationServiceTests()
    {
        _registry = new RegistryAccess(_testKey + @"\Classes");
        _service = new ShellIntegrationService(_registry);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_testKey, throwOnMissingSubKey: false);
        using var root = Registry.CurrentUser.OpenSubKey(TestsRoot, writable: false);
        if (root is { SubKeyCount: 0, ValueCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(TestsRoot, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Register_WritesThreeLocationsWithQuotedCommands()
    {
        // Act
        _service.Register(ExePath);

        // Assert — 02 §8.1 표
        Assert.Equal(AppConstants.ShellMenuText, _registry.GetValue(@"Directory\shell\MDFileViewer", null));
        Assert.Equal($"{ExePath},0", _registry.GetValue(@"Directory\shell\MDFileViewer", "Icon"));
        Assert.Equal("Single", _registry.GetValue(@"Directory\shell\MDFileViewer", "MultiSelectModel"));
        Assert.Equal($"\"{ExePath}\" \"%1\"", _registry.GetValue(@"Directory\shell\MDFileViewer\command", null));
        Assert.Equal($"\"{ExePath}\" \"%V\"", _registry.GetValue(@"Directory\Background\shell\MDFileViewer\command", null));
        Assert.Equal($"\"{ExePath}\" \"%1\"", _registry.GetValue(@"SystemFileAssociations\.md\shell\MDFileViewer\command", null));
        Assert.Equal($"\"{ExePath}\" \"%1\"", _registry.GetValue(@"SystemFileAssociations\.markdown\shell\MDFileViewer\command", null));
    }

    [Fact]
    public void Unregister_RemovesOnlyOwnKeys()
    {
        // Arrange — 다른 프로그램의 메뉴는 남아야 한다
        _service.Register(ExePath);
        _registry.SetValue(@"Directory\shell\OtherApp", null, "다른 앱");

        // Act
        _service.Unregister();

        // Assert
        Assert.Null(_registry.GetValue(@"Directory\shell\MDFileViewer", null));
        Assert.Null(_registry.GetValue(@"Directory\Background\shell\MDFileViewer", null));
        Assert.Null(_registry.GetValue(@"SystemFileAssociations\.md\shell\MDFileViewer", null));
        Assert.Null(_registry.GetValue(@"SystemFileAssociations\.markdown\shell\MDFileViewer", null));
        Assert.Equal("다른 앱", _registry.GetValue(@"Directory\shell\OtherApp", null));
    }

    [Fact]
    public void Unregister_WhenNothingRegistered_DoesNotThrow()
    {
        // Act & Assert
        _service.Unregister();
        Assert.IsType<ShellRegistrationStatus.NotRegistered>(_service.GetStatus(ExePath));
    }

    [Fact]
    public void GetStatus_DistinguishesSameAndOtherExe()
    {
        // Arrange
        _service.Register(@"D:\Old\MDFileViewer.exe");

        // Act
        var elsewhere = _service.GetStatus(ExePath);
        _service.Register(ExePath);
        var registered = _service.GetStatus(ExePath.ToUpperInvariant());

        // Assert — 01 F-13 exe 위치 변경
        Assert.Equal(new ShellRegistrationStatus.RegisteredElsewhere(@"D:\Old\MDFileViewer.exe"), elsewhere);
        Assert.IsType<ShellRegistrationStatus.Registered>(registered);
    }

    [Fact]
    public void GetStatus_SomeOfOwnKeysMissing_IsIncomplete()
    {
        // Arrange — 리뷰(2026-11-18): 폴더 키만 보고 "등록됨"이라 했는데 배경·파일 키가 없거나 옛 경로일 수 있었다
        _service.Register(ExePath);
        _registry.DeleteTree(@"Directory\Background\shell\MDFileViewer");

        // Act & Assert
        Assert.IsType<ShellRegistrationStatus.Incomplete>(_service.GetStatus(ExePath));
    }

    [Fact]
    public void GetStatus_FileKeyPointsToOtherExe_IsRegisteredElsewhere()
    {
        // Arrange
        _service.Register(ExePath);
        _registry.SetValue(@"SystemFileAssociations\.md\shell\MDFileViewer\command", null, "\"D:\\Old\\MDFileViewer.exe\" \"%1\"");

        // Act & Assert
        Assert.Equal(new ShellRegistrationStatus.RegisteredElsewhere(@"D:\Old\MDFileViewer.exe"), _service.GetStatus(ExePath));
    }

    [Fact]
    public void Register_FailsMidway_RemovesKeysItWrote()
    {
        // Arrange — 리뷰(2026-11-18): 중간에 실패하면 일부만 등록된 채 남았다
        var failing = new FailAfterWritesRegistry(_registry, allowedWrites: 5);
        var service = new ShellIntegrationService(failing);

        // Act
        Assert.Throws<UnauthorizedAccessException>(() => service.Register(ExePath));

        // Assert
        Assert.IsType<ShellRegistrationStatus.NotRegistered>(_service.GetStatus(ExePath));
        Assert.Null(_registry.GetValue(@"Directory\shell\MDFileViewer", null));
    }

    [Fact]
    public void ShellVerbKeyName_IsNotEmpty()
    {
        // Assert — 보안 리뷰(2026-11-18): 비면 해제가 Directory\shell 전체를 지운다
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.ShellVerbKeyName));
        Assert.DoesNotContain('\\', AppConstants.ShellVerbKeyName);
    }

    [Fact]
    public void GetStatus_ForeignCommandFormat_IsNotRegistered()
    {
        // Arrange — 같은 키 이름을 다른 형식으로 쓴 경우
        _registry.SetValue(@"Directory\shell\MDFileViewer\command", null, "notepad.exe %1");

        // Act & Assert
        Assert.IsType<ShellRegistrationStatus.NotRegistered>(_service.GetStatus(ExePath));
    }

    // 정해진 횟수만 쓰고 그다음 쓰기는 권한 오류를 낸다(정책으로 막힌 키를 흉내)
    private sealed class FailAfterWritesRegistry(IRegistryAccess inner, int allowedWrites) : IRegistryAccess
    {
        private int _writes;

        public void SetValue(string keyPath, string? name, string value)
        {
            if (++_writes > allowedWrites)
            {
                throw new UnauthorizedAccessException("막힌 키");
            }

            inner.SetValue(keyPath, name, value);
        }

        public string? GetValue(string keyPath, string? name) => inner.GetValue(keyPath, name);

        public void DeleteTree(string keyPath) => inner.DeleteTree(keyPath);
    }
}
