namespace MDFileViewer.Core.Models;

/// <summary>
/// 드라이브 하나를 확인하는 방법. <see cref="DriveInfo"/>는 sealed라 응답 지연·실패를 테스트에서 재현할 수 없어서
/// 확인 동작을 함수로 받는다.
/// </summary>
public sealed record DriveProbe(string RootPath, DriveType DriveType, Func<bool> IsReady, Func<string?> ReadVolumeLabel);
