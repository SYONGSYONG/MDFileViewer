using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 탐색기 우클릭 메뉴 "MDFileViewer로 열기"를 현재 사용자에게 등록·해제한다 (01 F-13, 02 §8.1 표). 앱이 만든 키만 지운다.
/// </summary>
public sealed class ShellIntegrationService(IRegistryAccess registry)
{
    private const string FolderKey = @"Directory\shell\" + AppConstants.ShellVerbKeyName;
    private const string BackgroundKey = @"Directory\Background\shell\" + AppConstants.ShellVerbKeyName;
    private const string CommandSubKey = @"\command";
    private const string IconValueName = "Icon";
    private const string MultiSelectValueName = "MultiSelectModel";
    private const string SingleSelection = "Single";

    // 폴더·파일은 선택한 항목(%1), 배경은 지금 보고 있는 폴더(%V)를 넘긴다
    private const string ItemArgument = "%1";
    private const string BackgroundArgument = "%V";
    private const char Quote = '"';

    private static readonly string[] FileKeys =
    [
        @"SystemFileAssociations\.md\shell\" + AppConstants.ShellVerbKeyName,
        @"SystemFileAssociations\.markdown\shell\" + AppConstants.ShellVerbKeyName,
    ];

    /// <summary>네 곳에 모두 쓴다. 중간에 실패하면 앱의 키를 지우고 예외를 다시 던진다 — 일부만 등록된 채 남지 않게 한다.</summary>
    public void Register(string exePath)
    {
        try
        {
            WriteVerb(FolderKey, exePath, ItemArgument, isSingleSelection: true);
            WriteVerb(BackgroundKey, exePath, BackgroundArgument, isSingleSelection: false);
            foreach (var fileKey in FileKeys)
            {
                WriteVerb(fileKey, exePath, ItemArgument, isSingleSelection: true);
            }
        }
        catch
        {
            Unregister();
            throw;
        }
    }

    public void Unregister()
    {
        foreach (var key in AllKeys)
        {
            registry.DeleteTree(key);
        }
    }

    /// <summary>
    /// 네 곳의 명령에서 exe 경로를 꺼내 지금 exe와 비교한다 (02 §8.2). 폴더 키만 보면 배경·파일 키가 없거나 옛 경로여도 "등록됨"이 됐다(리뷰 2026-11-18).
    /// </summary>
    public ShellRegistrationStatus GetStatus(string exePath)
    {
        string?[] registered = [.. AllKeys.Select(key => registry.GetValue(key + CommandSubKey, name: null) is { } command ? ExtractExePath(command) : null)];
        if (registered.All(path => path is null))
        {
            return new ShellRegistrationStatus.NotRegistered();
        }

        // 둘 다 등록할 때·지금 쓰는 전체 경로(Environment.ProcessPath)라 정규화 없이 비교한다
        if (registered.FirstOrDefault(path => path is not null && !string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase)) is { } other)
        {
            return new ShellRegistrationStatus.RegisteredElsewhere(other);
        }

        return registered.Any(path => path is null) ? new ShellRegistrationStatus.Incomplete() : new ShellRegistrationStatus.Registered();
    }

    private static IEnumerable<string> AllKeys => [FolderKey, BackgroundKey, .. FileKeys];

    private void WriteVerb(string key, string exePath, string argument, bool isSingleSelection)
    {
        registry.SetValue(key, name: null, AppConstants.ShellMenuText);
        registry.SetValue(key, IconValueName, $"{exePath},0");
        if (isSingleSelection)
        {
            registry.SetValue(key, MultiSelectValueName, SingleSelection);
        }

        registry.SetValue(key + CommandSubKey, name: null, $"{Quote}{exePath}{Quote} {Quote}{argument}{Quote}");
    }

    // "<exe>" "%1" 형식의 첫 따옴표 묶음. 형식이 다르면(다른 프로그램이 같은 이름으로 썼다) 등록되지 않은 것으로 본다.
    private static string? ExtractExePath(string command)
    {
        if (command.Length < 2 || command[0] != Quote)
        {
            return null;
        }

        var end = command.IndexOf(Quote, 1);
        if (end <= 1)
        {
            return null;
        }

        var path = command[1..end];
        return path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ? null : path;
    }
}
