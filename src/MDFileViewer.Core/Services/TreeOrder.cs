using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 트리 자식의 순서 (01 F-02, 02 §7.1): 하위 폴더를 모두 먼저, 그다음 md 파일. 각각 이름순(현재 문화권, 대소문자 무시 — 한글 이름 고려).
/// 서비스가 읽은 목록과, 목록에 없던 항목을 트리에 끼워 넣는 자리가 같은 규칙을 쓰게 한곳에 둔다.
/// </summary>
internal static class TreeOrder
{
    public static List<TreeEntry> Sort(IEnumerable<TreeEntry> entries) =>
        [.. entries.Order(Comparer<TreeEntry>.Create((left, right) => Compare(left.Kind, left.DisplayName, right.Kind, right.DisplayName)))];

    public static int Compare(TreeNodeKind leftKind, string leftName, TreeNodeKind rightKind, string rightName)
    {
        var byGroup = Group(leftKind).CompareTo(Group(rightKind));

        // 문화권은 비교할 때마다 읽는다 — 정적 필드에 담아 두면 처음 쓴 순간의 문화권에 묶인다
        return byGroup != 0 ? byGroup : StringComparer.CurrentCultureIgnoreCase.Compare(leftName, rightName);
    }

    private static int Group(TreeNodeKind kind) => kind == TreeNodeKind.MarkdownFile ? 1 : 0;
}
