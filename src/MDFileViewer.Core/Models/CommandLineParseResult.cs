namespace MDFileViewer.Core.Models;

/// <summary>시작 요청과, 쓰지 않고 넘긴 인수(로그에 남긴다).</summary>
public sealed record CommandLineParseResult(StartupRequest Request, IReadOnlyList<string> IgnoredArguments);
