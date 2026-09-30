#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

sealed class CloudSaveFieldFilter
{
    public string Text = string.Empty;
    public bool MatchEmpty;

    public bool IsActive =>
        MatchEmpty || !string.IsNullOrWhiteSpace(Text);

    public void Clear()
    {
        Text = string.Empty;
        MatchEmpty = false;
    }
}

sealed class CloudSaveUsersTreeView : TreeView
{
    public const string DeleteColumnKey = "__delete__";

    readonly List<string> _columnKeys;
    readonly List<Dictionary<string, string>> _rows;
    readonly Action<string> _onDeleteRequested;
    readonly Func<bool> _isInteractionEnabled;
    string _filter = string.Empty;
    IReadOnlyDictionary<string, CloudSaveFieldFilter> _fieldFilters;

    public CloudSaveUsersTreeView(
        TreeViewState state,
        MultiColumnHeader multiColumnHeader,
        List<string> columnKeys,
        List<Dictionary<string, string>> rows,
        Action<string> onDeleteRequested,
        Func<bool> isInteractionEnabled)
        : base(state, multiColumnHeader)
    {
        _columnKeys = columnKeys;
        _rows = rows;
        _onDeleteRequested = onDeleteRequested;
        _isInteractionEnabled = isInteractionEnabled;
        rowHeight = 22f;
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        multiColumnHeader.sortingChanged += _ => SortRows();
        Reload();
    }

    public void SetFilter(string filter)
    {
        _filter = filter ?? string.Empty;
        Reload();
        Repaint();
    }

    public void SetFieldFilters(IReadOnlyDictionary<string, CloudSaveFieldFilter> fieldFilters)
    {
        _fieldFilters = fieldFilters;
        Reload();
        Repaint();
    }

    public void SetFilters(string globalFilter, IReadOnlyDictionary<string, CloudSaveFieldFilter> fieldFilters)
    {
        _filter = globalFilter ?? string.Empty;
        _fieldFilters = fieldFilters;
        Reload();
        Repaint();
    }

    public bool HasActiveFilter() =>
        !string.IsNullOrEmpty(_filter) || HasActiveFieldFilters();

    bool HasActiveFieldFilters()
    {
        if (_fieldFilters == null || _fieldFilters.Count == 0)
            return false;

        foreach (var pair in _fieldFilters)
        {
            if (pair.Value != null && pair.Value.IsActive)
                return true;
        }

        return false;
    }

    public int GetVisibleRowCount()
    {
        if (!HasActiveFilter())
            return _rows.Count;

        var count = 0;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (RowMatchesFilter(_rows[i]))
                count++;
        }

        return count;
    }

    public List<Dictionary<string, string>> GetVisibleRows()
    {
        if (!HasActiveFilter())
            return new List<Dictionary<string, string>>(_rows);

        var visible = new List<Dictionary<string, string>>();
        for (var i = 0; i < _rows.Count; i++)
        {
            if (RowMatchesFilter(_rows[i]))
                visible.Add(_rows[i]);
        }

        return visible;
    }

    /// <summary>
    /// Columns currently shown in the TreeView (visible headers), excluding Delete.
    /// Order matches the multi-column header left-to-right.
    /// </summary>
    public List<string> GetVisibleExportColumns()
    {
        var columns = new List<string>();
        if (_columnKeys == null || multiColumnHeader?.state?.columns == null)
            return columns;

        var visibleIndices = multiColumnHeader.state.visibleColumns;
        if (visibleIndices == null || visibleIndices.Length == 0)
        {
            for (var i = 0; i < _columnKeys.Count; i++)
            {
                if (_columnKeys[i] != DeleteColumnKey)
                    columns.Add(_columnKeys[i]);
            }

            return columns;
        }

        for (var v = 0; v < visibleIndices.Length; v++)
        {
            var index = visibleIndices[v];
            if (index < 0 || index >= _columnKeys.Count)
                continue;

            var key = _columnKeys[index];
            if (key != DeleteColumnKey)
                columns.Add(key);
        }

        return columns;
    }

    public Dictionary<string, string> GetSelectedRow()
    {
        var selection = GetSelection();
        if (selection == null || selection.Count == 0)
            return null;

        var id = selection[0];
        if (id < 0 || id >= _rows.Count)
            return null;

        return _rows[id];
    }

    public int GetSelectedRowCount()
    {
        var selection = GetSelection();
        if (selection == null || selection.Count == 0)
            return 0;

        var count = 0;
        for (var i = 0; i < selection.Count; i++)
        {
            var id = selection[i];
            if (id >= 0 && id < _rows.Count)
                count++;
        }

        return count;
    }

    public void ReloadRows()
    {
        Reload();
        Repaint();
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        Repaint();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
        var items = new List<TreeViewItem>(_rows.Count);

        for (var i = 0; i < _rows.Count; i++)
        {
            if (!RowMatchesFilter(_rows[i]))
                continue;

            var playerId = GetCellValue(_rows[i], "playerId");
            items.Add(new TreeViewItem
            {
                id = i,
                depth = 0,
                displayName = string.IsNullOrEmpty(playerId) ? $"Row {i + 1}" : playerId
            });
        }

        SetupParentsAndChildrenFromDepths(root, items);
        return root;
    }

    bool RowMatchesFilter(Dictionary<string, string> row)
    {
        if (!MatchesGlobalFilter(row))
            return false;

        return MatchesFieldFilters(row);
    }

    bool MatchesGlobalFilter(Dictionary<string, string> row)
    {
        if (string.IsNullOrEmpty(_filter))
            return true;

        if (row == null || row.Count == 0)
            return false;

        foreach (var value in row.Values)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            if (value.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    bool MatchesFieldFilters(Dictionary<string, string> row)
    {
        if (_fieldFilters == null || _fieldFilters.Count == 0)
            return true;

        foreach (var pair in _fieldFilters)
        {
            var fieldFilter = pair.Value;
            if (fieldFilter == null || !fieldFilter.IsActive)
                continue;

            var value = GetCellValue(row, pair.Key);
            if (fieldFilter.MatchEmpty)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return false;
                continue;
            }

            if (string.IsNullOrEmpty(value)
                || value.IndexOf(fieldFilter.Text.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        return true;
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        if (args.item.id < 0 || args.item.id >= _rows.Count)
            return;

        var row = _rows[args.item.id];
        for (var visible = 0; visible < args.GetNumVisibleColumns(); visible++)
        {
            var cellRect = args.GetCellRect(visible);
            var columnIndex = args.GetColumn(visible);
            if (columnIndex < 0 || columnIndex >= _columnKeys.Count)
                continue;

            var columnKey = _columnKeys[columnIndex];
            if (columnKey == DeleteColumnKey)
            {
                CenterRectUsingSingleLineHeight(ref cellRect);
                var playerId = GetCellValue(row, "playerId");
                using (new EditorGUI.DisabledScope(_isInteractionEnabled != null && !_isInteractionEnabled()))
                {
                    if (GUI.Button(cellRect, "Delete") && !string.IsNullOrWhiteSpace(playerId))
                        _onDeleteRequested?.Invoke(playerId);
                }

                continue;
            }

            var value = GetCellValue(row, columnKey);
            value = CloudSaveTimeFormat.FormatFieldForDisplay(columnKey, value);
            DrawCellLabel(cellRect, value, args.item, columnIndex, args.selected, args.focused);
        }
    }

    void DrawCellLabel(Rect cellRect, string value, TreeViewItem item, int columnIndex, bool selected, bool focused)
    {
        if (string.IsNullOrEmpty(value))
            return;

        CenterRectUsingSingleLineHeight(ref cellRect);

        const float horizontalPadding = 4f;
        cellRect.xMin += horizontalPadding;
        cellRect.xMax -= horizontalPadding;

        if (columnIndex == columnIndexForTreeFoldouts)
            cellRect.xMin += GetContentIndent(item);

        if (cellRect.width <= 1f)
            return;

        var style = DefaultStyles.label;
        var displayText = Ellipsize(value, style, cellRect.width);
        if (Event.current.type == EventType.Repaint)
            style.Draw(cellRect, new GUIContent(displayText, value), false, false, selected, focused);
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

    void SortRows()
    {
        var sortedColumn = multiColumnHeader.sortedColumnIndex;
        if (sortedColumn < 0 || sortedColumn >= _columnKeys.Count)
            return;

        var key = _columnKeys[sortedColumn];
        if (key == DeleteColumnKey)
            return;
        var ascending = multiColumnHeader.IsSortedAscending(sortedColumn);
        _rows.Sort((left, right) =>
        {
            var leftValue = GetCellValue(left, key);
            var rightValue = GetCellValue(right, key);
            var compare = CloudSaveTimeFormat.CompareField(key, leftValue, rightValue);
            return ascending ? compare : -compare;
        });

        Reload();
    }

    public static MultiColumnHeaderState CreateDefaultHeaderState(IReadOnlyList<string> columnKeys)
    {
        var columns = new MultiColumnHeaderState.Column[columnKeys.Count];
        for (var i = 0; i < columnKeys.Count; i++)
        {
            var key = columnKeys[i];
            var isDeleteColumn = key == DeleteColumnKey;
            columns[i] = new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(isDeleteColumn ? "Delete" : key),
                width = isDeleteColumn ? 64 : GetDefaultColumnWidth(key),
                minWidth = isDeleteColumn ? 64 : GetMinColumnWidth(key),
                autoResize = false,
                allowToggleVisibility = !isDeleteColumn,
                canSort = !isDeleteColumn
            };
        }

        var state = new MultiColumnHeaderState(columns);
        return state;
    }

    static int GetDefaultColumnWidth(string key) =>
        key switch
        {
            "playerId" => 280,
            "pushToken" => 280,
            "metadata" => 200,
            "naming" => 180,
            "time_utc_user" => 180,
            "date_utc_server" => 100,
            "time_utc_server" => 180,
            "hour" => 56,
            "month" => 56,
            "day" => 56,
            "time_stamp_1" => 100,
            "time_stamp_2" => 100,
            "time_stamp_3" => 100,
            "total" => 90,
            "finalLink" => 280,
            "gpu" => 140,
            "cpu" => 140,
            "appId" => 160,
            "deviceID" => 200,
            "campaign" => 160,
            "network" => 100,
            "isPrivacy" => 72,
            "isWhiteList" => 72,
            "isThrusted" => 72,
            "devModel" => 140,
            _ => 120
        };

    static int GetMinColumnWidth(string key) =>
        key switch
        {
            "playerId" => 160,
            "pushToken" => 120,
            "metadata" => 100,
            _ => 72
        };

    static string GetCellValue(Dictionary<string, string> row, string key) =>
        row != null && row.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
}
#endif
