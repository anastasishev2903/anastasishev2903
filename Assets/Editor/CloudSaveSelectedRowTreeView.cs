#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

sealed class CloudSaveSelectedRowTreeView : TreeView
{
    public const string CopyColumnKey = "__copy__";

    readonly List<DetailEntry> _entries = new();

    public CloudSaveSelectedRowTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader)
        : base(state, multiColumnHeader)
    {
        rowHeight = 22f;
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        multiColumnHeader.sortingChanged += _ => SortEntries();
        Reload();
    }

    public void SetEntries(List<DetailEntry> entries)
    {
        _entries.Clear();
        if (entries != null)
            _entries.AddRange(entries);

        Reload();
        Repaint();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
        var items = new List<TreeViewItem>(_entries.Count);

        for (var i = 0; i < _entries.Count; i++)
        {
            items.Add(new TreeViewItem
            {
                id = i,
                depth = 0,
                displayName = _entries[i].Key
            });
        }

        SetupParentsAndChildrenFromDepths(root, items);
        return root;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        if (args.item.id < 0 || args.item.id >= _entries.Count)
            return;

        var entry = _entries[args.item.id];
        for (var visible = 0; visible < args.GetNumVisibleColumns(); visible++)
        {
            var cellRect = args.GetCellRect(visible);
            var columnIndex = args.GetColumn(visible);
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (columnIndex)
            {
                case 0:
                    DrawCellLabel(cellRect, entry.Key, args.selected, args.focused);
                    break;
                case 1:
                    DrawCellLabel(cellRect, entry.DisplayValue, args.selected, args.focused);
                    break;
                case 2:
                    if (GUI.Button(cellRect, "Copy") && !string.IsNullOrEmpty(entry.DisplayValue))
                        EditorGUIUtility.systemCopyBuffer = entry.DisplayValue;
                    break;
            }
        }
    }

    void DrawCellLabel(Rect cellRect, string value, bool selected, bool focused)
    {
        if (string.IsNullOrEmpty(value))
            return;

        const float horizontalPadding = 4f;
        cellRect.xMin += horizontalPadding;
        cellRect.xMax -= horizontalPadding;
        if (cellRect.width <= 1f)
            return;

        var style = DefaultStyles.label;
        var displayText = Ellipsize(value, style, cellRect.width);
        if (Event.current.type == EventType.Repaint)
            style.Draw(cellRect, new GUIContent(displayText, value), false, false, selected, focused);
    }

    void SortEntries()
    {
        var sortedColumn = multiColumnHeader.sortedColumnIndex;
        if (sortedColumn < 0 || sortedColumn > 1)
            return;

        var ascending = multiColumnHeader.IsSortedAscending(sortedColumn);
        _entries.Sort((left, right) =>
        {
            var leftValue = sortedColumn == 0 ? left.Key : left.DisplayValue;
            var rightValue = sortedColumn == 0 ? right.Key : right.DisplayValue;
            var compare = string.CompareOrdinal(leftValue ?? string.Empty, rightValue ?? string.Empty);
            return ascending ? compare : -compare;
        });

        Reload();
    }

    public static MultiColumnHeaderState CreateDefaultHeaderState()
    {
        return new MultiColumnHeaderState(new[]
        {
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Field"),
                width = 160f,
                minWidth = 100f,
                autoResize = false,
                allowToggleVisibility = false,
                canSort = true
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Value"),
                width = 420f,
                minWidth = 120f,
                autoResize = true,
                allowToggleVisibility = false,
                canSort = true
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Copy"),
                width = 64f,
                minWidth = 64f,
                autoResize = false,
                allowToggleVisibility = false,
                canSort = false
            }
        });
    }

    static string Ellipsize(string text, GUIStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxWidth <= 0f)
            return "…";

        if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
            return text;

        const string ellipsis = "…";
        var ellipsisWidth = style.CalcSize(new GUIContent(ellipsis)).x;
        var targetWidth = maxWidth - ellipsisWidth;
        if (targetWidth <= 0f)
            return ellipsis;

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var size = style.CalcSize(new GUIContent(text.Substring(0, mid))).x;
            if (size <= targetWidth)
                low = mid;
            else
                high = mid - 1;
        }

        return low <= 0 ? ellipsis : text.Substring(0, low) + ellipsis;
    }

    public readonly struct DetailEntry
    {
        public readonly string Key;
        public readonly string DisplayValue;

        public DetailEntry(string key, string displayValue)
        {
            Key = key ?? string.Empty;
            DisplayValue = displayValue ?? string.Empty;
        }
    }
}
#endif
