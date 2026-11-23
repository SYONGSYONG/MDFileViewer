using System.Security.AccessControl;
using System.Security.Principal;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

public sealed class NamedMutexTests
{
    [Fact]
    public void TryOpen_NewName_ReturnsMutex()
    {
        // Arrange
        var logger = new FakeAppLogger();

        // Act
        using var mutex = NamedMutex.TryOpen(UniqueName(), logger);

        // Assert
        Assert.NotNull(mutex);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void TryOpen_NameHeldByAccessibleMutex_ReturnsMutex()
    {
        // Arrange
        var name = UniqueName();
        var logger = new FakeAppLogger();
        using var existing = new Mutex(initiallyOwned: false, name);

        // Act
        using var mutex = NamedMutex.TryOpen(name, logger);

        // Assert
        Assert.NotNull(mutex);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Constructor_NameHeldByMutexDeniedToCurrentUser_Throws()
    {
        // Arrange — 권한이 다른 실행이 만든 뮤텍스를 흉내 낸다. 이 테스트는 "막힌 뮤텍스를 열면 예외가 난다"는 전제를 붙잡아 둔다
        var name = UniqueName();
        using var denied = CreateDeniedToCurrentUser(name);

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => new Mutex(initiallyOwned: false, name));
    }

    [Fact]
    public void TryOpen_NameHeldByMutexDeniedToCurrentUser_ReturnsNullAndWarns()
    {
        // Arrange
        var name = UniqueName();
        var logger = new FakeAppLogger();
        using var denied = CreateDeniedToCurrentUser(name);

        // Act
        var mutex = NamedMutex.TryOpen(name, logger);

        // Assert
        Assert.Null(mutex);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("WARN", entry.Level);
        Assert.IsType<UnauthorizedAccessException>(entry.Exception);
    }

    private static string UniqueName() => $"MDFileViewer.Tests.NamedMutex.{Guid.NewGuid():N}";

    // 만든 쪽은 핸들을 받지만, 그 뒤에 이름으로 여는 쪽(같은 사용자)은 거부된다
    private static Mutex CreateDeniedToCurrentUser(string name)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(identity.User!, MutexRights.FullControl, AccessControlType.Deny));
        return MutexAcl.Create(initiallyOwned: false, name, out _, security);
    }
}
