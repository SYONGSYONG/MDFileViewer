using System.Security.AccessControl;
using System.Security.Principal;

namespace MDFileViewer.Core.Tests;

/// <summary>
/// 현재 사용자에게 폴더 목록 읽기를 잠시 거부해 "접근 거부" 경로를 재현한다. Dispose에서 거부 규칙을 지운다.
/// </summary>
internal sealed class FolderAccessDenial : IDisposable
{
    private readonly DirectoryInfo _directory;
    private readonly FileSystemAccessRule _rule;

    public FolderAccessDenial(string folderPath)
    {
        // 백업 권한이 켜져 있으면 거부 규칙이 무시되므로 먼저 끈다 (ProcessPrivileges 설명 참고)
        ProcessPrivileges.DisableAclBypass();
        _directory = new DirectoryInfo(folderPath);
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("현재 사용자 SID를 알 수 없습니다.");
        _rule = new FileSystemAccessRule(currentUser, FileSystemRights.ListDirectory, AccessControlType.Deny);

        var security = _directory.GetAccessControl();
        security.AddAccessRule(_rule);
        _directory.SetAccessControl(security);
    }

    public void Dispose()
    {
        var security = _directory.GetAccessControl();
        security.RemoveAccessRule(_rule);
        _directory.SetAccessControl(security);
    }
}
