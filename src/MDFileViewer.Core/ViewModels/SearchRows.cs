using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>검색 결과 목록의 한 줄 — 파일 줄이거나 찾은 줄이다 (01 F-31, 03 §2.9).</summary>
public abstract class SearchRowViewModel : ObservableObject
{
    /// <summary>화면 낭독기가 읽을 말.</summary>
    public abstract string AccessibleName { get; }
}

/// <summary>파일 줄: "상대 경로 (n곳)". 누르면 그 파일의 찾은 줄들을 접거나 편다.</summary>
public sealed class SearchFileRowViewModel : SearchRowViewModel
{
    private bool _isExpanded = true;

    public SearchFileRowViewModel(string rootFolder, FolderSearchFileResult result)
    {
        FilePath = result.FilePath;
        TotalLines = result.TotalLines;
        Header = string.Format(UiText.SearchFileHeaderFormat, Path.GetRelativePath(rootFolder, result.FilePath), result.Hits.Count);
        Lines = [.. result.Hits.Select(hit => new SearchLineRowViewModel(this, hit))];
    }

    public string FilePath { get; }

    public int TotalLines { get; }

    public string Header { get; }

    public IReadOnlyList<SearchLineRowViewModel> Lines { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        internal set => SetProperty(ref _isExpanded, value);
    }

    public override string AccessibleName => Header;
}

/// <summary>찾은 줄: "줄 번호: 내용". 누르면 그 문서를 연다.</summary>
public sealed class SearchLineRowViewModel : SearchRowViewModel
{
    public SearchLineRowViewModel(SearchFileRowViewModel file, FolderSearchHit hit)
    {
        File = file;
        LineNumber = hit.LineNumber;
        Preview = hit.Preview;
    }

    public SearchFileRowViewModel File { get; }

    public int LineNumber { get; }

    public string Preview { get; }

    public override string AccessibleName => string.Format(UiText.SearchLineAutomationFormat, LineNumber, Preview);
}

/// <summary>사용자가 고른 결과 줄 — 문서를 열고 그 높이로 스크롤하고 찾는 말을 칠할 때 쓴다.</summary>
public sealed record SearchChoice(string FilePath, int LineNumber, int TotalLines, string Term, bool MatchCase);
