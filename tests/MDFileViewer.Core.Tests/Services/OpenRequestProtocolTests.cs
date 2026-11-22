using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>열기 요청 한 줄 형식 (01 F-23, 02 §17.14).</summary>
public sealed class OpenRequestProtocolTests
{
    [Fact]
    public void SerializeThenParse_RoundTripsArgumentsAndWorkingDirectory()
    {
        // Arrange — 한글·공백·따옴표·줄 바꿈이 든 인수도 한 줄에 담겨야 한다
        var message = new OpenRequestMessage([@"D:\문서 폴더\가이드.md", "a\"b", "둘째\n줄"], @"C:\작업 폴더");

        // Act
        var line = OpenRequestProtocol.Serialize(message);
        var parsed = OpenRequestProtocol.TryParse(line, out var result);

        // Assert
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.True(parsed);
        Assert.Equal(message.Arguments, result!.Arguments);
        Assert.Equal(message.WorkingDirectory, result.WorkingDirectory);
    }

    [Fact]
    public void SerializeThenParse_NoArguments_RoundTripsEmptyList()
    {
        // Arrange — 인수 없는 실행은 창만 앞으로 올린다
        var message = new OpenRequestMessage([], @"C:\");

        // Act
        var parsed = OpenRequestProtocol.TryParse(OpenRequestProtocol.Serialize(message), out var result);

        // Assert
        Assert.True(parsed);
        Assert.Empty(result!.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("글자")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"v\":1,\"args\":[\"a\"]}")]
    [InlineData("{\"v\":1,\"args\":[\"a\"],\"cwd\":\"\"}")]
    [InlineData("{\"v\":1,\"args\":\"a\",\"cwd\":\"C:\\\\\"}")]
    [InlineData("{\"v\":1,\"args\":[null],\"cwd\":\"C:\\\\\"}")]
    [InlineData("{\"v\":1,\"args\":[1],\"cwd\":\"C:\\\\\"}")]
    public void TryParse_MalformedLine_ReturnsFalse(string? line)
    {
        // Act
        var parsed = OpenRequestProtocol.TryParse(line, out var result);

        // Assert
        Assert.False(parsed);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_OtherFormatVersion_ReturnsFalse()
    {
        // Arrange — 형식이 다른 판(다른 버전의 포터블 exe)의 요청은 읽지 않는다. 보낸 쪽이 자기 창을 연다
        const string line = "{\"v\":2,\"args\":[],\"cwd\":\"C:\\\\\"}";

        // Act
        var parsed = OpenRequestProtocol.TryParse(line, out _);

        // Assert
        Assert.False(parsed);
    }
}
