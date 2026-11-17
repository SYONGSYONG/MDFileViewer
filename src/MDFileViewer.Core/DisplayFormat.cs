using System.Globalization;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core;

/// <summary>목록 열과 상태 표시줄이 같은 모양으로 보여 주도록 값을 문자열로 바꾼다 (03 §2.1 상태 표시줄 예시).</summary>
public static class DisplayFormat
{
    private const long BytesPerKilobyte = 1024;
    private const long BytesPerMegabyte = BytesPerKilobyte * 1024;
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm";
    private const int SizeDecimals = 1;

    /// <summary>`512 B`, `4.2 KB`, `12.3 MB`.</summary>
    public static string FormatSize(long sizeBytes)
    {
        if (sizeBytes < BytesPerKilobyte)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{sizeBytes} B");
        }

        // 반올림한 값으로 단위를 정한다 — 1,048,575 B를 "1024 KB"로 보이지 않게 한다
        var kilobytes = Math.Round(sizeBytes / (double)BytesPerKilobyte, SizeDecimals, MidpointRounding.AwayFromZero);
        return kilobytes < BytesPerKilobyte
            ? string.Create(CultureInfo.InvariantCulture, $"{kilobytes:0.#} KB")
            : string.Create(CultureInfo.InvariantCulture, $"{sizeBytes / (double)BytesPerMegabyte:0.#} MB");
    }

    /// <summary>큰 파일 경고 문구용 MB 값.</summary>
    public static double ToMegabytes(long sizeBytes) => sizeBytes / (double)BytesPerMegabyte;

    /// <summary>`2026-11-17 15:30` — 지정한 시간대(보통 PC의 현지 시간대)로 바꿔 보여 준다.</summary>
    public static string FormatDateTime(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string FormatEncoding(TextEncodingKind encoding) => encoding switch
    {
        TextEncodingKind.Utf8 => "UTF-8",
        TextEncodingKind.Utf8Bom => "UTF-8 (BOM)",
        TextEncodingKind.Utf16LittleEndian => "UTF-16 LE",
        TextEncodingKind.Utf16BigEndian => "UTF-16 BE",
        TextEncodingKind.Cp949 => "CP949",
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "알 수 없는 인코딩입니다."),
    };

    public static string FormatLineEnding(LineEnding lineEnding) => lineEnding switch
    {
        LineEnding.CrLf => "CRLF",
        LineEnding.Lf => "LF",
        _ => throw new ArgumentOutOfRangeException(nameof(lineEnding), lineEnding, "알 수 없는 줄 끝입니다."),
    };
}
