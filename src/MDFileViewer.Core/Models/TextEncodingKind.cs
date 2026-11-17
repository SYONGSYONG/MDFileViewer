namespace MDFileViewer.Core.Models;

/// <summary>파일을 읽을 때 판별한 인코딩. 저장할 때 같은 형식으로 되돌리려고 BOM 유무까지 구분한다.</summary>
public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16LittleEndian,
    Utf16BigEndian,
    Cp949,
}
