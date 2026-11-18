namespace MDFileViewer.Core.Models;

/// <summary>시작 요청과, 쓰지 않고 넘긴 인수(로그에 남긴다).</summary>
/// <param name="IsSilent">`--silent` — 등록·해제 결과 창을 띄우지 않는다(설치 파일 제거 단계, 01 F-01).</param>
public sealed record CommandLineParseResult(StartupRequest Request, IReadOnlyList<string> IgnoredArguments, bool IsSilent = false);
