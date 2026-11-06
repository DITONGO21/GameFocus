using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace GameFocus.Infrastructure.GameSources;

// Read-only access to HKEY_LOCAL_MACHINE, so launcher sources can be tested with a fake.
public interface IWindowsRegistry
{
    IEnumerable<string> GetSubKeyNames(string keyPath);

    string? GetString(string keyPath, string valueName);
}

// Key paths starting with "HKCU\" are read from the current user hive; all others from HKLM.
public class WindowsRegistry : IWindowsRegistry
{
    private const string CurrentUserPrefix = @"HKCU\";

    public IEnumerable<string> GetSubKeyNames(string keyPath)
    {
        try
        {
            using var key = Open(keyPath);
            return key?.GetSubKeyNames().ToList() ?? new List<string>();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    public string? GetString(string keyPath, string valueName)
    {
        try
        {
            using var key = Open(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static RegistryKey? Open(string keyPath)
    {
        return keyPath.StartsWith(CurrentUserPrefix, StringComparison.OrdinalIgnoreCase)
            ? Registry.CurrentUser.OpenSubKey(keyPath.Substring(CurrentUserPrefix.Length))
            : Registry.LocalMachine.OpenSubKey(keyPath);
    }
}
