namespace MDFileViewer.Core.Models;

/// <summary>저장 위치를 묻는 대화상자에 넘길 값 (01 F-26). <paramref name="Extension"/>은 점을 뺀 확장자다.</summary>
public sealed record SaveFileRequest(string Title, string SuggestedFileName, string FilterName, string Extension, string? InitialFolder);
