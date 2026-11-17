using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests;

public sealed class DisplayFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(4300, "4.2 KB")]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(1024 * 1024 - 1, "1 MB")] // 반올림하면 1024 KB가 되는 크기는 MB로 보여 준다
    [InlineData(12_900_000, "12.3 MB")]
    public void FormatSize_UsesBinaryUnitsWithOneDecimal(long size, string expected)
    {
        // Act
        var text = DisplayFormat.FormatSize(size);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void FormatDateTime_ConvertsToGivenTimeZone()
    {
        // Arrange
        var utc = new DateTimeOffset(2026, 11, 17, 6, 30, 0, TimeSpan.Zero);
        var korea = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");

        // Act
        var text = DisplayFormat.FormatDateTime(utc, korea);

        // Assert
        Assert.Equal("2026-11-17 15:30", text);
    }

    [Theory]
    [InlineData(TextEncodingKind.Utf8, "UTF-8")]
    [InlineData(TextEncodingKind.Utf8Bom, "UTF-8 (BOM)")]
    [InlineData(TextEncodingKind.Utf16LittleEndian, "UTF-16 LE")]
    [InlineData(TextEncodingKind.Utf16BigEndian, "UTF-16 BE")]
    [InlineData(TextEncodingKind.Cp949, "CP949")]
    public void FormatEncoding_NamesEachKind(TextEncodingKind encoding, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, DisplayFormat.FormatEncoding(encoding));
    }

    [Theory]
    [InlineData(LineEnding.CrLf, "CRLF")]
    [InlineData(LineEnding.Lf, "LF")]
    public void FormatLineEnding_NamesEachKind(LineEnding lineEnding, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, DisplayFormat.FormatLineEnding(lineEnding));
    }

    [Fact]
    public void Format_UndefinedEnumValues_Throw()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => DisplayFormat.FormatEncoding((TextEncodingKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => DisplayFormat.FormatLineEnding((LineEnding)99));
    }
}
