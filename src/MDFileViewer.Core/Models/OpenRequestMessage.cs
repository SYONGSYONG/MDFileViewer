namespace MDFileViewer.Core.Models;

/// <summary>
/// 나중에 실행된 앱이 이미 열린 창에 넘기는 열기 요청 (01 F-23, 02 §17.14). 받은 인수를 해석하지 않고 그대로 넘겨, 받는 쪽이 시작할 때와 같은
/// 해석기(<see cref="Services.CommandLineParser"/>)로 푼다.
/// </summary>
/// <param name="Arguments">나중에 실행된 앱이 받은 명령줄 인수.</param>
/// <param name="WorkingDirectory">그 앱의 작업 폴더 — 상대 경로 인수의 기준이다.</param>
public sealed record OpenRequestMessage(IReadOnlyList<string> Arguments, string WorkingDirectory);
