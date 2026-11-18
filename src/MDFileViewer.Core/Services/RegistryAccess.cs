using MDFileViewer.Core.Abstractions;
using Microsoft.Win32;

namespace MDFileViewer.Core.Services;

/// <summary><c>HKEY_CURRENT_USER\&lt;rootPath&gt;</c> 아래를 읽고 쓴다. 관리자 권한이 필요 없다 (01 F-13).</summary>
public sealed class RegistryAccess(string rootPath) : IRegistryAccess
{
    /// <summary>실제 셸 통합 루트 — <c>HKCU\Software\Classes</c>(HKEY_CLASSES_ROOT에 합쳐져 보인다, 02 §8.1).</summary>
    public const string ClassesRoot = @"Software\Classes";

    public void SetValue(string keyPath, string? name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Combine(keyPath), writable: true);
        key.SetValue(name ?? string.Empty, value, RegistryValueKind.String);
    }

    public string? GetValue(string keyPath, string? name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Combine(keyPath), writable: false);
        return key?.GetValue(name ?? string.Empty) as string;
    }

    public void DeleteTree(string keyPath) => Registry.CurrentUser.DeleteSubKeyTree(Combine(keyPath), throwOnMissingSubKey: false);

    private string Combine(string keyPath) => rootPath + @"\" + keyPath;
}
