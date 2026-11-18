using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeAppLogger _logger = new();
    private readonly string _path;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _path = Path.Combine(_temp.Root, "sub", AppConstants.SettingsFileName);
        _store = new JsonSettingsStore(_path, _logger);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        // Act
        var settings = _store.Load();

        // Assert
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.Null(settings.Window);
        Assert.Equal(AppConstants.DefaultTreePanelWidth, settings.Layout.TreePanelWidth);
        Assert.Empty(settings.Favorites);
        Assert.Empty(settings.Tabs.Files);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        // Arrange — 01 F-14 저장 항목
        var saved = new AppSettings
        {
            Window = new WindowSettings(120, 80, 1440, 900, IsMaximized: true),
            Layout = new LayoutSettings { TreePanelWidth = 400, EditorSplitRatio = 0.6 },
            Theme = ThemeMode.Dark,
            LastFolder = @"D:\Docs",
            Editor = new EditorSettings { ShowLineNumbers = false, WordWrap = false },
            Favorites = [@"D:\Docs", @"C:\Notes"],
            Tabs = new TabsSettings([new TabSettings(@"D:\Docs\a.md", false), new TabSettings(@"D:\Docs\b.md", true)], 1),
        };

        // Act
        var written = _store.Save(saved);
        var loaded = _store.Load();

        // Assert
        Assert.True(written);
        Assert.Equal(saved.Window, loaded.Window);
        Assert.Equal(saved.Layout, loaded.Layout);
        Assert.Equal(saved.Theme, loaded.Theme);
        Assert.Equal(saved.LastFolder, loaded.LastFolder);
        Assert.Equal(saved.Editor, loaded.Editor);
        Assert.Equal(saved.Favorites, loaded.Favorites);
        Assert.Equal(saved.Tabs.Files, loaded.Tabs.Files);
        Assert.Equal(1, loaded.Tabs.ActiveIndex);
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(_path), StringComparison.Ordinal);
    }

    [Fact]
    public void Save_OverwritesExistingFileWithoutLeavingTempFile()
    {
        // Arrange
        _store.Save(new AppSettings { Theme = ThemeMode.Light });

        // Act
        _store.Save(new AppSettings { Theme = ThemeMode.Dark });

        // Assert
        Assert.Equal(ThemeMode.Dark, _store.Load().Theme);
        Assert.Equal([AppConstants.SettingsFileName], Directory.GetFiles(Path.GetDirectoryName(_path)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsBackup()
    {
        // Arrange — 01 F-14 손상
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not json");

        // Act
        var settings = _store.Load();

        // Assert
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.False(File.Exists(_path));
        Assert.Equal("{ not json", File.ReadAllText(_path + ".bak"));
        Assert.Single(_logger.Entries, entry => entry.Level == "WARN");
    }

    [Fact]
    public void Load_OutOfRangeValues_FallBackPerField()
    {
        // Arrange — 범위 밖 너비·비율, 빈 경로, 크기 0인 창
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """
            {
              "window": { "left": 0, "top": 0, "width": 0, "height": 500, "isMaximized": false },
              "layout": { "treePanelWidth": 10, "editorSplitRatio": 0.95 },
              "theme": "Light",
              "lastFolder": "  ",
              "favorites": ["", "D:\\Docs"],
              "tabs": { "files": [{ "path": "", "isPreview": false }, { "path": "D:\\a.md", "isPreview": true }], "activeIndex": 1 },
              "unknown": 42
            }
            """);

        // Act
        var settings = _store.Load();

        // Assert
        Assert.Null(settings.Window);
        Assert.Equal(AppConstants.DefaultTreePanelWidth, settings.Layout.TreePanelWidth);
        Assert.Equal(AppConstants.DefaultEditorSplitRatio, settings.Layout.EditorSplitRatio);
        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.Null(settings.LastFolder);
        Assert.Equal([@"D:\Docs"], settings.Favorites);
        Assert.Equal([new TabSettings(@"D:\a.md", true)], settings.Tabs.Files);
    }

    [Fact]
    public void Load_OneItemWithWrongTypeOrUnknownEnum_KeepsOtherItems()
    {
        // Arrange — 리뷰(2026-11-18): 모르는 테마 이름 하나 때문에 파일 전체가 .bak으로 가고 즐겨찾기·탭까지 초기화됐다
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """
            {
              "theme": "Sepia",
              "layout": "넓게",
              "favorites": ["D:\\Docs"],
              "tabs": { "files": [{ "path": "D:\\a.md", "isPreview": false }], "activeIndex": 0 }
            }
            """);

        // Act
        var settings = _store.Load();

        // Assert
        Assert.Equal(ThemeMode.System, settings.Theme);
        Assert.Equal(AppConstants.DefaultTreePanelWidth, settings.Layout.TreePanelWidth);
        Assert.Equal([@"D:\Docs"], settings.Favorites);
        Assert.Equal([new TabSettings(@"D:\a.md", false)], settings.Tabs.Files);
        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".bak"));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsDeclinedShellReregistration()
    {
        // Arrange — 2026-11-18 사용자 결정: 재등록 제안을 "나중에"로 거절한 exe 위치에서는 다시 묻지 않는다
        var saved = new AppSettings { DeclinedShellReregistrationExe = @"D:\Tools\MDFileViewer.exe" };

        // Act
        _store.Save(saved);

        // Assert
        Assert.Equal(saved.DeclinedShellReregistrationExe, _store.Load().DeclinedShellReregistrationExe);
    }
}
