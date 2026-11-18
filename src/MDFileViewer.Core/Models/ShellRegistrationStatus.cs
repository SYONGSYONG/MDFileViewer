namespace MDFileViewer.Core.Models;

/// <summary>탐색기 메뉴 등록 상태 (02 §8.2).</summary>
public abstract record ShellRegistrationStatus
{
    private ShellRegistrationStatus()
    {
    }

    public sealed record NotRegistered : ShellRegistrationStatus;

    /// <summary>지금 실행 중인 exe로 등록돼 있다.</summary>
    public sealed record Registered : ShellRegistrationStatus;

    /// <summary>다른 위치의 exe로 등록돼 있다 — 시작할 때 다시 등록할지 묻는다 (01 F-13 P1).</summary>
    public sealed record RegisteredElsewhere(string ExePath) : ShellRegistrationStatus;

    /// <summary>이 exe로 등록돼 있지만 키 일부가 없다(손으로 지웠거나 등록이 중간에 끊겼다) — 시작할 때 조용히 다시 등록한다.</summary>
    public sealed record Incomplete : ShellRegistrationStatus;
}
