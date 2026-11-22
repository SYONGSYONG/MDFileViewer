using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 열기 요청을 한 줄 글자로 바꾸고 되돌린다 (02 §17.14). 줄 하나가 요청 하나다 — JSON은 줄 바꿈을 이스케이프하므로 인수에 줄 바꿈이
/// 있어도 한 줄에 담긴다.
/// </summary>
public static class OpenRequestProtocol
{
    /// <summary>받는 쪽이 요청을 받아들였다는 답.</summary>
    public const string Accepted = "OK";

    /// <summary>받는 쪽이 요청을 읽지 못했다는 답. 보낸 쪽은 자기 창을 연다.</summary>
    public const string Rejected = "ERR";

    // 형식이 바뀌면 올린다. 다른 판의 요청은 읽지 않는다 — 설치본과 버전이 다른 포터블 exe를 함께 쓸 때 잘못 해석하지 않게
    private const int FormatVersion = 1;

    public static string Serialize(OpenRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(new Envelope(FormatVersion, [.. message.Arguments], message.WorkingDirectory));
    }

    public static bool TryParse(string? line, [NotNullWhen(true)] out OpenRequestMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(line);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is not { Version: FormatVersion, Arguments: { } arguments, WorkingDirectory: { Length: > 0 } workingDirectory }
            || arguments.Any(argument => argument is null))
        {
            return false;
        }

        message = new OpenRequestMessage(arguments!, workingDirectory);
        return true;
    }

    // 다른 프로세스가 보낸 글자라 빠진 항목·null이 올 수 있다 — 모두 null 허용으로 읽고 TryParse가 검사한다
    private sealed record Envelope(
        [property: JsonPropertyName("v")] int Version,
        [property: JsonPropertyName("args")] string?[]? Arguments,
        [property: JsonPropertyName("cwd")] string? WorkingDirectory);
}
