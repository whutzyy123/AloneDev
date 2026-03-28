using Microsoft.UI.Xaml.Input;
using PMTool.Core.Models.Settings;
using Windows.System;

namespace PMTool.App.Services;

public static class ShortcutBindingParser
{
    public static bool TryParse(string? text, out VirtualKey key, out VirtualKeyModifiers modifiers, out string? error)
    {
        key = VirtualKey.None;
        modifiers = VirtualKeyModifiers.None;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "快捷键不能为空。";
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "请使用组合键，例如 Ctrl+Shift+P。";
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var p = parts[i];
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= VirtualKeyModifiers.Control;
            }
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= VirtualKeyModifiers.Windows;
            }
            else
            {
                error = $"未知修饰键：{p}";
                return false;
            }
        }

        if (modifiers == VirtualKeyModifiers.None)
        {
            error = "请至少包含 Ctrl / Shift / Alt / Win 之一。";
            return false;
        }

        var last = parts[^1];
        if (!TryMapKeyToken(last, out key))
        {
            error = $"未知按键：{last}";
            return false;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu) &&
            modifiers.HasFlag(VirtualKeyModifiers.Control) &&
            key is VirtualKey.Delete)
        {
            error = "系统保留组合，无法使用。";
            return false;
        }

        return true;
    }

    public static string Format(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyToken(key));
        return string.Join('+', parts);
    }

    /// <summary>将用户录制到的组合规范化为与 config 一致的显示串。</summary>
    public static bool TryNormalizeDisplay(string? text, out string normalized, out string? error)
    {
        normalized = "";
        if (!TryParse(text, out var vk, out var mods, out error))
        {
            return false;
        }

        normalized = Format(vk, mods);
        return true;
    }

    private static bool TryMapKeyToken(string token, out VirtualKey key)
    {
        token = token.Trim();
        if (token.Length == 1)
        {
            var ch = char.ToUpperInvariant(token[0]);
            if (ch is >= 'A' and <= 'Z')
            {
                key = (VirtualKey)((int)VirtualKey.A + (ch - 'A'));
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                key = ch == '0'
                    ? VirtualKey.Number0
                    : (VirtualKey)((int)VirtualKey.Number1 + (ch - '1'));
                return true;
            }
        }

        return Enum.TryParse(token, ignoreCase: true, out key) && key != VirtualKey.None;
    }

    private static string KeyToken(VirtualKey key) =>
        key switch
        {
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)('0' + (key - VirtualKey.Number0))).ToString(),
            >= VirtualKey.A and <= VirtualKey.Z => ((char)('A' + (key - VirtualKey.A))).ToString(),
            _ => key.ToString(),
        };

    /// <summary>校验整表快捷键是否互斥；全局搜索强制 Ctrl+K。</summary>
    public static bool TryValidateShortcutTable(
        IReadOnlyDictionary<string, string> shortcuts,
        out string? error)
    {
        error = null;
        if (!shortcuts.TryGetValue(nameof(ShortcutActionId.GlobalSearch), out var gs))
        {
            error = "缺少全局搜索快捷键。";
            return false;
        }

        if (!TryNormalizeDisplay(gs, out var gsNorm, out var gsErr))
        {
            error = $"全局搜索：{gsErr}";
            return false;
        }

        if (!string.Equals(gsNorm, AppShortcutDefaults.GlobalSearch, StringComparison.OrdinalIgnoreCase))
        {
            error = $"全局搜索必须为 {AppShortcutDefaults.GlobalSearch}（PRD 要求）。";
            return false;
        }

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in shortcuts)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
            {
                error = $"{kv.Key} 的快捷键无效。";
                return false;
            }

            if (!TryNormalizeDisplay(kv.Value, out var norm, out var normErr))
            {
                error = $"{kv.Key}：{normErr}";
                return false;
            }

            if (seen.TryGetValue(norm, out var other))
            {
                error = $"快捷键冲突：「{kv.Key}」与「{other}」均为 {norm}。";
                return false;
            }

            seen[norm] = kv.Key;
        }

        return true;
    }
}
