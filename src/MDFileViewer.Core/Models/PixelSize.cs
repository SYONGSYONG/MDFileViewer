namespace MDFileViewer.Core.Models;

/// <summary>물리 픽셀 크기. 창 최소 크기를 모니터 배율로 바꾼 값 등에 쓴다 (02 §4.2).</summary>
public readonly record struct PixelSize(int Width, int Height);
