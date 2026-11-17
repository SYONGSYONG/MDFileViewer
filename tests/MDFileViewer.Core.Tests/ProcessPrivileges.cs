using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MDFileViewer.Core.Tests;

/// <summary>
/// 테스트 프로세스의 백업·복원 권한을 끈다.
/// Git Bash에서 띄운 프로세스는 관리자 토큰의 SeBackupPrivilege가 켜진 채로 실행된다(2026-11-17 확인, PowerShell에서는 꺼져 있음).
/// 이 권한이 켜져 있으면 폴더 핸들을 백업 방식으로 열 때 ACL 검사를 건너뛰어 "접근 거부" 테스트를 재현할 수 없다.
/// 실제 앱은 탐색기에서 실행되어 이 권한이 꺼져 있으므로, 테스트도 같은 조건으로 맞춘다.
/// </summary>
internal static class ProcessPrivileges
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint PrivilegeDisabled = 0;
    private const int ErrorNotAllAssigned = 1300;

    private static readonly string[] BypassingPrivileges = ["SeBackupPrivilege", "SeRestorePrivilege"];
    private static readonly Lazy<bool> Disabled = new(DisableBypassingPrivilegesCore);

    /// <summary>한 번만 실행된다. 프로세스 전체에 적용되며, 다른 테스트는 이 권한에 기대지 않는다.</summary>
    public static void DisableAclBypass() => _ = Disabled.Value;

    private static bool DisableBypassingPrivilegesCore()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "프로세스 토큰을 열지 못했습니다.");
        }

        using (token)
        {
            foreach (var privilege in BypassingPrivileges)
            {
                if (!LookupPrivilegeValue(null, privilege, out var luid))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"{privilege} 값을 찾지 못했습니다.");
                }

                var state = new TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = PrivilegeDisabled };
                var adjusted = AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero);
                var error = Marshal.GetLastWin32Error();

                // 토큰에 그 권한이 아예 없으면 ERROR_NOT_ALL_ASSIGNED — 끌 것이 없으니 정상이다
                if (!adjusted || (error != 0 && error != ErrorNotAllAssigned))
                {
                    throw new Win32Exception(error, $"{privilege}를 끄지 못했습니다.");
                }
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        SafeAccessTokenHandle tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TokenPrivileges newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);
}
