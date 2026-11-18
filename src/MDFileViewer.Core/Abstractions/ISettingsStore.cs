using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>설정 읽기·쓰기 (01 F-14, 02 §17.8). 예외를 내지 않는다 — 실패는 기본값·로그로 처리한다.</summary>
public interface ISettingsStore
{
    AppSettings Load();

    /// <returns>썼으면 <c>true</c>.</returns>
    bool Save(AppSettings settings);
}
