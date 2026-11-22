using System.Drawing;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 마우스 입력을 보내기 직전의 안전 확인 (02 §14.3). 앱 창이 맨 앞이 아니거나, 점이 앱 창 밖이거나, 다른 창이 그 점을 덮고 있으면
/// 입력이 다른 프로그램으로 가므로 테스트를 실패시켜 입력을 보내지 않는다.
/// </summary>
internal static class SafeInput
{
    public const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 다른 창에 입력이 가지 않도록 입력을 보내지 않습니다.";

    // 스크롤로 화면 밖에 나간 요소도 좌표는 돌려주므로(창 위·아래) 점 검사가 필요하다.
    public static void EnsureCanSendAt(AppSession session, Point point)
    {
        Assert.True(session.IsForeground, NotForegroundMessage);
        Assert.True(session.MainWindow.BoundingRectangle.Contains(point), $"누를 점 {point}이 앱 창 밖입니다 — 누르지 않습니다.");
        Assert.True(
            session.IsPointOnApp(point),
            $"누를 점 {point}을 다른 창({AppSession.DescribeWindowAt(point)})이 덮고 있습니다 — 누르지 않습니다.");
    }

    public static Point CenterOf(Rectangle bounds) => new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
}
