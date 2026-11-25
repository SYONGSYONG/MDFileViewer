using System.Runtime.InteropServices;

namespace MDFileViewer.Services;

/// <summary>
/// 프로세스를 정리 단계 없이 바로 끝낸다 (02 §13). 보통의 종료(<c>Main</c>이 돌아가거나 <see cref="Environment.Exit"/>)는 끝에서 불러온 DLL마다
/// 정리 코드를 돌리는데, 창을 띄우는 도중에 닫으면 그 단계에서 멈추는 일이 있었다 — 2026-11-25 재현: 72회 가운데 2회, 앱의 종료 처리는 끝났고
/// 스레드는 주 스레드 하나만 남아 기다리는 상태였으며 WebView2 프로세스가 딸려 있었다. 창 없는 프로세스가 남아 설치 프로그램이 앱이 켜져 있다고 본다.
/// 그 단계에서는 다른 스레드가 이미 없어 안에서 깨울 방법이 없으므로, 종료 처리를 마친 뒤 그 단계에 들어가기 전에 끝낸다.
/// </summary>
internal static class ProcessTerminator
{
    /// <summary>
    /// 지금 프로세스를 <paramref name="exitCode"/>로 끝낸다. 돌아오지 않는다. 저장할 것(설정·로그)을 모두 마친 뒤에 부른다 — 종료자와
    /// DLL 정리는 돌지 않는다.
    /// </summary>
    public static void Terminate(int exitCode)
    {
        // 실패하면(일어날 일이 거의 없다) 보통의 종료로 넘어간다
        if (!TerminateProcess(GetCurrentProcess(), unchecked((uint)exitCode)))
        {
            Environment.Exit(exitCode);
        }
    }

    // 프로젝트의 다른 P/Invoke(WindowFitter)와 같은 DllImport 방식을 쓴다
    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint processHandle, uint exitCode);
}
