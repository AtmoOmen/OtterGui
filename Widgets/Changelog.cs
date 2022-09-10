using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;

namespace OtterGui.Widgets;

public sealed class Changelog : Window
{
    public const int FreshInstallVersion = int.MaxValue;

    public const uint DefaultHeaderColor    = 0xFF60D0D0;
    public const uint DefaultHighlightColor = 0xFF6060FF;

    private readonly Func<int>                   _getLastVersion;
    private readonly Action<int>                 _setLastVersion;
    private readonly List<(string, List<Entry>)> _entries = new();

    private int _lastVersion;

    public uint HeaderColor { get; set; } = DefaultHeaderColor;
    public bool ForceOpen   { get; set; } = false;

    public Changelog(string label, Func<int> getLastVersion, Action<int> setLastVersion)
        : base(label, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize,
            true)
    {
        _getLastVersion    = getLastVersion;
        _setLastVersion    = setLastVersion;
        Position           = null;
        RespectCloseHotkey = false;
        ShowCloseButton    = false;
    }

    public override void PreOpenCheck()
    {
        _lastVersion = _getLastVersion();
        if (_lastVersion == FreshInstallVersion)
        {
            IsOpen = false;
            _setLastVersion(_entries.Count);
        }
        else
        {
            IsOpen = ForceOpen || _lastVersion < _entries.Count;
        }
    }

    public override void PreDraw()
    {
        Size = new Vector2(Math.Min(ImGui.GetMainViewport().Size.X / ImGuiHelpers.GlobalScale / 2, 800),
            ImGui.GetMainViewport().Size.Y / ImGuiHelpers.GlobalScale / 2);
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport((ImGui.GetMainViewport().Size - Size.Value * ImGuiHelpers.GlobalScale) / 2,
            ImGuiCond.Appearing);
    }

    public override void Draw()
    {
        using (var child = ImRaii.Child("Entries", new Vector2(-1, -ImGui.GetFrameHeight() * 2)))
        {
            var i = 0;
            foreach (var ((name, list), idx) in _entries.WithIndex().Reverse())
            {
                using var id    = ImRaii.PushId(i++);
                using var color = ImRaii.PushColor(ImGuiCol.Text, HeaderColor);
                var       flags = ImGuiTreeNodeFlags.NoTreePushOnOpen;
                if (idx >= _lastVersion || idx == _entries.Count - 1)
                    flags |= ImGuiTreeNodeFlags.DefaultOpen;

                var tree = ImGui.TreeNodeEx(name, flags);
                CopyToClipboard(name, list);
                color.Pop();
                if (tree)
                    foreach (var entry in list)
                        entry.Draw();
            }
        }

        var pos = Size!.Value.X * ImGuiHelpers.GlobalScale / 3;
        ImGui.SetCursorPosX(pos);
        if (ImGui.Button("Understood", new Vector2(pos, 0)))
        {
            if (_lastVersion != _entries.Count)
                _setLastVersion(_entries.Count);
            ForceOpen = false;
        }
    }

    public Changelog NextVersion(string title)
    {
        _entries.Add((title, new List<Entry>()));
        return this;
    }

    public Changelog RegisterHighlight(string text, ushort level = 0, uint color = DefaultHighlightColor)
    {
        _entries.Last().Item2.Add(new Entry(text, color, level));
        return this;
    }

    public Changelog RegisterEntry(string text, ushort level = 0)
    {
        _entries.Last().Item2.Add(new Entry(text, 0, level));
        return this;
    }

    private readonly struct Entry
    {
        public readonly string Text;
        public readonly uint   Color;
        public readonly ushort SubText;

        public Entry(string text, uint color = 0, ushort subText = 0)
        {
            Text    = text;
            Color   = color;
            SubText = subText;
        }

        public void Draw()
        {
            using var tab   = ImRaii.PushIndent(1 + SubText);
            using var color = ImRaii.PushColor(ImGuiCol.Text, Color, Color != 0);
            ImGui.Bullet();
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(Text);
            ImGui.PopTextWrapPos();
        }

        public void Append(StringBuilder sb)
        {
            sb.Append("> ");
            if (SubText > 0)
                sb.Append('`');
            for (var i = 0; i < SubText; ++i)
                sb.Append("    ");
            if (SubText > 0)
                sb.Append('`');
            if (Color != 0)
                sb.Append("**");
            sb.Append("- ")
                .Append(Text);
            if (Color != 0)
                sb.Append("**");

            sb.Append('\n');
        }
    }

    [Conditional("DEBUG")]
    private static void CopyToClipboard(string name, List<Entry> entries)
    {
        try
        {
            if (!ImGui.IsItemClicked(ImGuiMouseButton.Right))
                return;

            var sb = new StringBuilder(1024 * 64);
            sb.Append("**")
                .Append(name)
                .Append(" notes, Update <t:")
                .Append(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .Append(">**\n");

            foreach (var entry in entries)
                entry.Append(sb);

            ImGui.SetClipboardText(sb.ToString());
        }
        catch
        {
            // ignored
        }
    }
}
