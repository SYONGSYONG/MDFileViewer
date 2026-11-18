using System.Text.Json;
using System.Text.Json.Serialization;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// <c>settings.json</c>을 읽고 쓴다 (02 §10·§17.8). 읽을 수 없는 파일은 <c>.bak</c>으로 옮기고 기본값으로 시작한다. 쓰기는 같은 폴더의 임시 파일에
/// 먼저 쓴 뒤 바꿔 끼워, 쓰는 도중 끝나도 이전 설정이 깨지지 않게 한다.
/// </summary>
public sealed class JsonSettingsStore(string filePath, IAppLogger logger) : ISettingsStore
{
    private const string BackupExtension = ".bak";
    private const string TempExtension = ".tmp";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>기본 위치 <c>%APPDATA%\MDFileViewer\settings.json</c> (01 F-14).</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), UiText.AppName, AppConstants.SettingsFileName);

    public AppSettings Load()
    {
        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(filePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("설정 파일의 최상위가 객체가 아닙니다.");
            }

            return Sanitize(ReadItems(document.RootElement));
        }
        catch (JsonException exception)
        {
            logger.Warn($"설정 파일을 읽을 수 없어 기본값으로 시작합니다: {filePath}", exception);
            MoveToBackup();
            return new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 잠시 잠긴 파일은 손상이 아니므로 옮기지 않는다
            logger.Warn($"설정 파일을 열지 못해 기본값으로 시작합니다: {filePath}", exception);
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        // 창을 여러 개 띄우면 같은 파일에 함께 쓴다 — 임시 파일 이름이 겹치지 않게 한다(TextFileCodec과 같은 방식)
        var tempPath = $"{filePath}.{Guid.NewGuid():N}{TempExtension}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            WriteDurably(tempPath, JsonSerializer.Serialize(settings, Options));
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Warn($"설정을 저장하지 못했습니다: {filePath}", exception);
            TryDelete(tempPath);
            return false;
        }
    }

    // 바꿔 끼우기 전에 디스크까지 내려 쓴다 — 전원이 끊기면 내용 없는 파일로 바뀐 설정이 남을 수 있다
    private static void WriteDurably(string path, string json)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: -1, leaveOpen: true))
        {
            writer.Write(json);
        }

        stream.Flush(flushToDisk: true);
    }

    // 항목마다 따로 읽는다 — 손으로 고친 파일에서 한 항목의 형식이 틀리거나 모르는 테마 이름이 있어도 그 항목만 기본값으로 둔다
    // (파일 전체를 버리면 즐겨찾기·탭까지 사라졌다, 리뷰 2026-11-18). 파일 자체가 JSON이 아닐 때만 손상으로 본다.
    private AppSettings ReadItems(JsonElement root)
    {
        var settings = new AppSettings();
        foreach (var property in root.EnumerateObject())
        {
            try
            {
                settings = ApplyItem(settings, property);
            }
            catch (JsonException exception)
            {
                logger.Warn($"설정 항목 '{property.Name}'을(를) 읽을 수 없어 기본값을 씁니다: {filePath}", exception);
            }
        }

        return settings;
    }

    private static AppSettings ApplyItem(AppSettings settings, JsonProperty property) => property.Name.ToUpperInvariant() switch
    {
        "VERSION" => settings with { Version = Read<int>(property) },
        "WINDOW" => settings with { Window = Read<WindowSettings?>(property) },
        "LAYOUT" => settings with { Layout = Read<LayoutSettings>(property) },
        "THEME" => settings with { Theme = Read<ThemeMode>(property) },
        "LASTFOLDER" => settings with { LastFolder = Read<string?>(property) },
        "EDITOR" => settings with { Editor = Read<EditorSettings>(property) },
        "FAVORITES" => settings with { Favorites = Read<List<string>>(property) },
        "TABS" => settings with { Tabs = Read<TabsSettings>(property) },
        "DECLINEDSHELLREREGISTRATIONEXE" => settings with { DeclinedShellReregistrationExe = Read<string?>(property) },
        _ => settings,
    };

    private static T Read<T>(JsonProperty property) => property.Value.Deserialize<T>(Options)!;

    // 손으로 고친 파일·이전 형식의 범위 밖 값은 그 항목만 기본값으로 되돌린다
    private static AppSettings Sanitize(AppSettings settings)
    {
        var defaults = new LayoutSettings();
        var layout = settings.Layout ?? defaults;
        var width = double.IsFinite(layout.TreePanelWidth) && layout.TreePanelWidth >= AppConstants.MinTreePanelWidth
            ? layout.TreePanelWidth
            : defaults.TreePanelWidth;
        var ratio = layout.EditorSplitRatio is >= AppConstants.MinEditorSplitRatio and <= AppConstants.MaxEditorSplitRatio
            ? layout.EditorSplitRatio
            : defaults.EditorSplitRatio;
        var tabs = settings.Tabs ?? TabsSettings.Empty;

        return settings with
        {
            Window = IsUsable(settings.Window) ? settings.Window : null,
            Layout = new LayoutSettings { TreePanelWidth = width, EditorSplitRatio = ratio },
            Theme = Enum.IsDefined(settings.Theme) ? settings.Theme : ThemeMode.System,
            LastFolder = string.IsNullOrWhiteSpace(settings.LastFolder) ? null : settings.LastFolder,
            Editor = settings.Editor ?? new EditorSettings(),
            Favorites = [.. (settings.Favorites ?? []).Where(path => !string.IsNullOrWhiteSpace(path))],
            Tabs = new TabsSettings([.. (tabs.Files ?? []).Where(tab => tab is not null && !string.IsNullOrWhiteSpace(tab.Path))], tabs.ActiveIndex),
            DeclinedShellReregistrationExe = string.IsNullOrWhiteSpace(settings.DeclinedShellReregistrationExe) ? null : settings.DeclinedShellReregistrationExe,
        };
    }

    private static bool IsUsable(WindowSettings? window) =>
        window is not null
        && double.IsFinite(window.Left) && double.IsFinite(window.Top)
        && double.IsFinite(window.Width) && double.IsFinite(window.Height)
        && window.Width > 0 && window.Height > 0;

    private void MoveToBackup()
    {
        try
        {
            File.Move(filePath, filePath + BackupExtension, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Warn($"손상된 설정 파일을 옮기지 못했습니다: {filePath}", exception);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Info($"임시 설정 파일을 지우지 못했습니다: {path} ({exception.GetType().Name})");
        }
    }
}
