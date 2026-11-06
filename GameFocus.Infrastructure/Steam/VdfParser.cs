using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GameFocus.Infrastructure.Steam;

// Small VDF/ACF parser tolerant to legacy and modern Steam formats.
// Not a full spec implementation but resilient for libraryfolders.vdf and appmanifest_*.acf.
public static class VdfParser
{
    // Parse VDF content into a nested dictionary
    public static Dictionary<string, object> Parse(string content)
    {
        var tokens = Tokenize(content);
        var index = 0;
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (index < tokens.Count)
        {
            var key = tokens[index++];
            if (index < tokens.Count && tokens[index] == "{")
            {
                index++; // skip '{'
                var obj = ParseObject(tokens, ref index);
                root[key] = obj;
                // expect a closing '}' already consumed by ParseObject
            }
            else if (index < tokens.Count)
            {
                var val = tokens[index++];
                root[key] = val;
            }
        }
        return root;
    }

    private static Dictionary<string, object> ParseObject(List<string> tokens, ref int index)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (index < tokens.Count)
        {
            var token = tokens[index++];
            if (token == "}")
                return dict;

            var key = token;
            if (index < tokens.Count && tokens[index] == "{")
            {
                index++; // skip '{'
                var obj = ParseObject(tokens, ref index);
                dict[key] = obj;
            }
            else if (index < tokens.Count)
            {
                var val = tokens[index++];
                dict[key] = val;
            }
        }
        return dict;
    }

    private static List<string> Tokenize(string s)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(s)) return tokens;
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (s[i] == '{' || s[i] == '}')
            {
                tokens.Add(s[i].ToString());
                i++; continue;
            }
            if (s[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    if (s[i] == '\\' && i + 1 < s.Length && (s[i + 1] == '"' || s[i + 1] == '\\'))
                    {
                        i++;
                        sb.Append(s[i]);
                        i++;
                        continue;
                    }
                    if (s[i] == '"') { i++; break; }
                    sb.Append(s[i]); i++;
                }
                tokens.Add(sb.ToString());
                continue;
            }
            // fallback: read until whitespace
            var j = i;
            while (j < s.Length && !char.IsWhiteSpace(s[j]) && s[j] != '{' && s[j] != '}') j++;
            tokens.Add(s.Substring(i, j - i));
            i = j;
        }
        return tokens;
    }

    // Helpers for common Steam files
    public static List<string> GetLibraryPathsFromVdf(string content, string steamInstallPath)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(content)) return paths;

        try
        {
            var root = Parse(content);
            // Modern format: libraryfolders -> { "0" { "path" "D:\\..." } }
            if (root.TryGetValue("libraryfolders", out var lfObj) && lfObj is Dictionary<string, object> lfDict)
            {
                foreach (var kv in lfDict)
                {
                    if (kv.Value is Dictionary<string, object> entry)
                    {
                        if (entry.TryGetValue("path", out var p) && p is string ps)
                        {
                            paths.Add(ps);
                        }
                    }
                    else if (kv.Value is string s)
                    {
                        // older-style numeric entries
                        paths.Add(s);
                    }
                }
            }
            else
            {
                // legacy flat key/value pairs: "1" "D:\\SteamLibrary"
                foreach (var kv in root)
                {
                    if (int.TryParse(kv.Key, out _ ) && kv.Value is string s)
                    {
                        paths.Add(s);
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        // always add steamInstallPath as first fallback
        if (!string.IsNullOrEmpty(steamInstallPath) && !paths.Contains(steamInstallPath, StringComparer.OrdinalIgnoreCase))
            paths.Insert(0, steamInstallPath);

        return paths;
    }

    public static Dictionary<string, string> ParseAppManifest(string content)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
            return result;

        try
        {
            var keys = new[]
            {
                "appid",
                "name",
                "installdir"
            };

            foreach (var key in keys)
            {
                var pattern =
                    $"\"{Regex.Escape(key)}\"\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"";

                var match = Regex.Match(
                    content,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var value = match.Groups["value"].Value
                        .Replace("\\\\", "\\")
                        .Replace("\\\"", "\"");

                    result[key] = value;
                }
            }
        }
        catch
        {
            // Ignora manifests inválidos
        }

        return result;
    }
}