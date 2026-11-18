using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

internal sealed class FakeShellLauncher : IShellLauncher
{
    public List<Uri> Opened { get; } = [];

    public List<string> Revealed { get; } = [];

    public List<string> OpenedFolders { get; } = [];

    public List<string> Copied { get; } = [];

    public Exception? OpenFailure { get; set; }

    public void OpenExternal(Uri uri)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        Opened.Add(uri);
    }

    public void RevealInExplorer(string path) => Revealed.Add(path);

    public void OpenFolder(string folderPath) => OpenedFolders.Add(folderPath);

    public void CopyToClipboard(string text) => Copied.Add(text);
}
