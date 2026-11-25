using System.Runtime.CompilerServices;
using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>폴더마다 정해 둔 파일 목록을 내는 가짜. <see cref="Gate"/>를 두면 그 파일 수만큼 낸 뒤 테스트가 풀어 줄 때까지 멈춘다.</summary>
internal sealed class FakeMarkdownFileFinder : IMarkdownFileFinder
{
    private readonly Dictionary<string, IReadOnlyList<string>> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>찾기를 요청받은 폴더(순서대로).</summary>
    public List<string> Requests { get; } = [];

    /// <summary>이 수만큼 낸 뒤 <see cref="Gate"/>를 기다린다. <c>null</c>이면 멈추지 않는다.</summary>
    public int? PauseAfter { get; set; }

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>파일을 다 낸 뒤 폴더 수 상한에 닿았다고 알린다.</summary>
    public bool ReachesFolderLimit { get; set; }

    /// <summary>다음 찾기가 파일을 다 낸 뒤 던질 예외.</summary>
    public Exception? Failure { get; set; }

    public void SetFiles(string rootFolder, params string[] relativePaths) =>
        _files[rootFolder] = relativePaths.Select(relative => Path.Combine(rootFolder, relative)).ToList();

    public async IAsyncEnumerable<string> FindAsync(
        string rootFolder, [EnumeratorCancellation] CancellationToken cancellationToken, Action? onFolderLimitReached = null)
    {
        Requests.Add(rootFolder);
        var count = 0;
        foreach (var path in _files.GetValueOrDefault(rootFolder, []))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (count == PauseAfter)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }

            count++;
            yield return path;
        }

        if (ReachesFolderLimit)
        {
            onFolderLimitReached?.Invoke();
        }

        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
