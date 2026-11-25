namespace MDFileViewer.Core.Models;

/// <summary>편집기에서 단축키나 도구 모음으로 적용하는 서식 (01 F-28).</summary>
public enum MarkdownFormat
{
    /// <summary><c>**글**</c></summary>
    Bold,

    /// <summary><c>*글*</c></summary>
    Italic,

    /// <summary><c>~~글~~</c></summary>
    Strikethrough,

    /// <summary><c>`글`</c></summary>
    Code,

    /// <summary><c>[글](주소)</c></summary>
    Link,

    /// <summary>제목 기호를 뗀 본문.</summary>
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,

    /// <summary><c>- 항목</c></summary>
    BulletList,

    /// <summary><c>1. 항목</c></summary>
    NumberedList,

    /// <summary><c>- [ ] 할 일</c></summary>
    TaskList,

    /// <summary><c>&gt; 인용</c></summary>
    Quote,
}
