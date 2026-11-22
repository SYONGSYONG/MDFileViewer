using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>설정 읽기·쓰기 (01 F-14, 02 §17.8). 예외를 내지 않는다 — 실패는 기본값·로그로 처리한다.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    /// <summary>
    /// 지금 파일에 있는 설정을 읽기만 한다 — 손상된 파일을 옮기지 않는다. 파일이 없으면 기본값, 읽지 못했으면 <c>null</c>.
    /// 실행 중에 다른 창이 바꾼 값을 볼 때 쓴다(읽지 못한 것을 기본값으로 착각하지 않게 <c>null</c>로 구분한다).
    /// </summary>
    AppSettings? TryLoad();

    /// <returns>썼으면 <c>true</c>.</returns>
    bool Save(AppSettings settings);
}
