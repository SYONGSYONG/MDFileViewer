using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 앱이 띄운 자식 프로세스를 찾는다. 테스트가 끝낼 프로세스를 "이 앱의 자식"으로만 좁혀 사용자가 쓰는 다른 프로그램을 건드리지 않게 한다.
/// </summary>
internal static class ProcessTree
{
    // NtQueryInformationProcess의 ProcessBasicInformation 정보 종류 번호(winternl.h)
    private const int ProcessBasicInformationClass = 0;

    // 부모 프로세스 번호만 읽으므로 가장 낮은 조회 권한으로 연다
    private const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>
    /// <paramref name="parent"/>가 띄운, 이름이 <paramref name="processName"/>인 프로세스들. 부모가 끝난 뒤 같은 번호를 다시 받은
    /// 프로세스와 헷갈리지 않도록 부모보다 늦게 시작한 것만 고른다. 호출자가 돌려받은 프로세스를 Dispose한다.
    /// </summary>
    public static IReadOnlyList<Process> FindChildren(Process parent, string processName)
    {
        var parentStart = parent.StartTime;
        var children = new List<Process>();
        foreach (var candidate in Process.GetProcessesByName(processName))
        {
            if (IsChildOf(candidate, parent.Id, parentStart))
            {
                children.Add(candidate);
            }
            else
            {
                candidate.Dispose();
            }
        }

        return children;
    }

    private static bool IsChildOf(Process candidate, int parentId, DateTime parentStart)
    {
        try
        {
            return TryGetParentId(candidate.Id, out var actualParentId)
                && actualParentId == parentId
                && candidate.StartTime >= parentStart;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // 다른 사용자의 프로세스이거나 확인하는 사이에 끝났다 — 이 앱의 자식이 아니다
            return false;
        }
    }

    private static bool TryGetParentId(int processId, out int parentId)
    {
        parentId = 0;
        using var handle = OpenProcess(ProcessQueryLimitedInformation, inheritHandle: false, processId);
        if (handle.IsInvalid)
        {
            return false;
        }

        var information = default(ProcessBasicInformation);
        var status = NtQueryInformationProcess(
            handle, ProcessBasicInformationClass, ref information, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        if (status != 0)
        {
            return false;
        }

        parentId = (int)information.InheritedFromUniqueProcessId;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle, int informationClass, ref ProcessBasicInformation information, int informationLength, out int returnLength);
}
