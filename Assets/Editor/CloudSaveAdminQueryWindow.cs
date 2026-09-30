#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Networking;

public sealed class CloudSaveAdminQueryWindow : EditorWindow
{
    const string PrefsPrefix = "CloudSaveAdminQuery_";
    const string CloudSaveAdminBaseUrl = "https://services.api.unity.com/cloud-save/v1";

    static readonly string[] ReturnKeys =
    {
        "playerId",
        "appId",
        "deviceID",
        "adId", "pushToken", "ip",
        "androidId", "devModel", "app_version", "install_mode",
        "install_store", "referer", "userAgent",
        "gpu", "cpu",
        "time_utc_user", "date_utc_server", "time_utc_server", "geo", "hour", "month", "day",
        "time_stamp_1", "time_stamp_2", "time_stamp_3",
        "total", "finalLink", "campaign", "network",
        "isPrivacy", "isWhiteList", "metadata", "naming"
    };

    static readonly string[] PreferredColumnOrder =
    {
        "playerId", "ip", "deviceID", "adId", "androidId", "devModel", "pushToken",
        "appId", "gpu", "cpu",
        "time_utc_user", "date_utc_server", "time_utc_server", "geo", "hour", "month", "day",
        "time_stamp_1", "time_stamp_2", "time_stamp_3",
        "total", "finalLink",
        "isPrivacy", "isWhiteList", "metadata",
        "app_version", "install_mode", "install_store", "referer", "campaign", "network", "userAgent",
        "naming",
        "error"
    };

    static readonly string[] PublicIndexFields =
    {
        "date_utc_server",
        "devModel",
        "adId",
        "deviceID",
        "androidId",
        "ip",
        "pushToken",
        "isPrivacy",
        "playerId",
        "campaign",
        "network",
        "hour",
        "month",
        "day"
    };

    const string TriggersAdminBaseUrl = "https://services.api.unity.com/triggers/v1";
    const string LeaderboardsAdminBaseUrl = "https://services.api.unity.com/leaderboards/v1";
    const string PlayerAuthAdminBaseUrl = "https://services.api.unity.com/player-identity/v1";
    const string LeaderboardsScoreSubmittedEventType = "com.unity.services.leaderboards.score-submitted.v1";
    const string CloudSaveKeySavedEventType = "com.unity.services.cloud-save.key-saved.v1";
    const string CloudCodeModuleName = "Module";

    static readonly CloudSaveTriggerSpec[] CloudSaveTriggers =
    {
        new CloudSaveTriggerSpec("payload", "payload", "Payload"),
        new CloudSaveTriggerSpec("load", "load", "Load"),
    };

    enum QueryMode
    {
        Search,
        ListAllPlayers
    }

    const int PaginationSafetyLimit = 100000;
    const float DefaultTreeViewHeight = 220f;
    const float MinTreeViewHeight = 120f;
    const float MaxTreeViewHeight = 720f;
    const float DefaultSelectedRowHeight = 140f;
    const float MinSelectedRowHeight = 60f;
    const float MaxSelectedRowHeight = 560f;
    const float DefaultStatusConsoleHeight = 80f;
    const float MinStatusConsoleHeight = 40f;
    const float MaxStatusConsoleHeight = 360f;
    const float AccountListRowHeight = 20f;
    const int AccountListVisibleRows = 8;
    const float ResizeHandleHeight = 8f;

    string _serviceKeyId = string.Empty;
    string _serviceSecretKey = string.Empty;
    string _bearerToken = string.Empty;
    bool _useBearerToken;

    string _projectId = string.Empty;
    string _environmentId = string.Empty;
    string _accountTitle = string.Empty;
    string _accountFilter = string.Empty;
    string _adjustToken = string.Empty;
    string _adjustS2sToken = string.Empty;
    readonly List<CloudSaveAdminAccount> _accounts = new();
    readonly HashSet<string> _deployAccountIds = new(StringComparer.Ordinal);
    string _selectedAccountId = string.Empty;

    string _adId = string.Empty;
    string _deviceId = string.Empty;
    string _androidId = string.Empty;
    string _ip = string.Empty;
    string _pushToken = string.Empty;
    string _devModel = string.Empty;
    string _dateUtcServer = string.Empty;
    string _playerIdFilter = string.Empty;
    string _isPrivacy = string.Empty;
    string _campaign = string.Empty;
    string _network = string.Empty;
    string _hour = string.Empty;
    string _month = string.Empty;
    string _day = string.Empty;
    bool _ipMatchMissing;
    bool _adIdMatchMissing;
    bool _deviceIdMatchMissing;
    bool _androidIdMatchMissing;
    bool _pushTokenMatchMissing;
    bool _devModelMatchMissing;
    bool _dateUtcServerMatchMissing;
    bool _playerIdMatchMissing;
    bool _isPrivacyMatchMissing;
    bool _campaignMatchMissing;
    bool _networkMatchMissing;
    bool _hourMatchMissing;
    bool _monthMatchMissing;
    bool _dayMatchMissing;
    string _deletePlayerId = string.Empty;
    QueryMode _queryMode = QueryMode.Search;
    int _resultLimit = 100;
    int _listPlayerLimit;

    string _lastStatusLogMessage = string.Empty;
    readonly List<string> _statusLog = new();
    bool _statusScrollToBottom = true;
    int? _playerManagementAmount;
    int? _cloudSaveEntityAmount;
    string _treeViewFilter = string.Empty;
    readonly Dictionary<string, CloudSaveFieldFilter> _fieldFilters = new(StringComparer.Ordinal);
    bool _fieldFiltersFoldout;
    Vector2 _windowScroll;
    Vector2 _accountListScroll;
    Vector2 _statusScroll;
    Vector2 _selectedRowScroll;
    float _statusConsoleHeight = DefaultStatusConsoleHeight;
    GUIStyle _statusConsoleStyle;
    static Texture2D _statusConsoleBackground;
    float _treeViewHeight = DefaultTreeViewHeight;
    float _selectedRowHeight = DefaultSelectedRowHeight;
    bool _draggingTreeSplitter;
    bool _draggingSelectedSplitter;
    bool _draggingStatusSplitter;
    bool _isBusy;
    CancellationTokenSource _fetchCts;

    readonly List<Dictionary<string, string>> _resultRows = new();
    List<string> _columnKeys = new();
    TreeViewState _treeViewState;
    MultiColumnHeaderState _treeHeaderState;
    CloudSaveUsersTreeView _usersTreeView;
    TreeViewState _selectedRowTreeState;
    MultiColumnHeaderState _selectedRowHeaderState;
    CloudSaveSelectedRowTreeView _selectedRowTreeView;
    string _selectedRowDetailsPlayerId = string.Empty;

    [MenuItem("Tools/Cloud Save Admin Query")]
    static void OpenWindow()
    {
        var window = GetWindow<CloudSaveAdminQueryWindow>("Cloud Save Admin Query");
        window.minSize = new Vector2(720f, 520f);
        window.Show();
    }

    void OnEnable()
    {
        LoadAuthPrefs();
        LoadAccountsFromJson();
        _resultLimit = EditorPrefs.GetInt(PrefsPrefix + "Limit", 100);
        _listPlayerLimit = EditorPrefs.GetInt(PrefsPrefix + "ListPlayerLimit", 0);
        _queryMode = (QueryMode)EditorPrefs.GetInt(PrefsPrefix + "Mode", (int)QueryMode.Search);
        _treeViewHeight = EditorPrefs.GetFloat(PrefsPrefix + "TreeHeight", DefaultTreeViewHeight);
        _selectedRowHeight = EditorPrefs.GetFloat(PrefsPrefix + "SelectedRowHeight", DefaultSelectedRowHeight);
        _statusConsoleHeight = EditorPrefs.GetFloat(PrefsPrefix + "StatusConsoleHeight", DefaultStatusConsoleHeight);
    }

    void OnDisable()
    {
        var cts = _fetchCts;
        _fetchCts = null;
        if (cts != null)
        {
            try
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
        }

        PersistSelectedAccount();
        SaveAuthPrefs();
        EditorPrefs.SetInt(PrefsPrefix + "Limit", _resultLimit);
        EditorPrefs.SetInt(PrefsPrefix + "ListPlayerLimit", _listPlayerLimit);
        EditorPrefs.SetInt(PrefsPrefix + "Mode", (int)_queryMode);
        EditorPrefs.SetFloat(PrefsPrefix + "TreeHeight", _treeViewHeight);
        EditorPrefs.SetFloat(PrefsPrefix + "SelectedRowHeight", _selectedRowHeight);
        EditorPrefs.SetFloat(PrefsPrefix + "StatusConsoleHeight", _statusConsoleHeight);
    }

    void OnGUI()
    {
        var maxStatus = GetMaxStatusConsoleHeight();
        _statusConsoleHeight = Mathf.Clamp(_statusConsoleHeight, MinStatusConsoleHeight, maxStatus);

        var maxSelected = GetMaxSelectedRowHeight();
        _selectedRowHeight = Mathf.Clamp(_selectedRowHeight, MinSelectedRowHeight, maxSelected);

        var statusHeight = GetStatusSectionLayoutHeight();
        var selectedHeight = GetSelectedRowSectionLayoutHeight();
        var workHeight = Mathf.Max(180f, position.height - statusHeight - selectedHeight);
        var contentWidth = position.width;

        GUILayout.BeginArea(new Rect(0f, 0f, contentWidth, workHeight));
        DrawWorkArea();
        GUILayout.EndArea();

        if (selectedHeight > 0f)
        {
            GUILayout.BeginArea(new Rect(0f, workHeight, contentWidth, selectedHeight));
            DrawSelectedRowDetails();
            GUILayout.EndArea();
        }

        GUILayout.BeginArea(new Rect(0f, workHeight + selectedHeight, contentWidth, statusHeight));
        DrawStatusConsole();
        GUILayout.EndArea();
    }

    bool ShouldDrawSelectedRowPane()
    {
        return _usersTreeView != null && _resultRows.Count > 0;
    }

    float GetSectionChromeHeight()
    {
        return EditorGUIUtility.singleLineHeight + 8f + ResizeHandleHeight + 10f;
    }

    float GetMaxStatusConsoleHeight()
    {
        var selectedMin = ShouldDrawSelectedRowPane()
            ? GetSectionChromeHeight() + MinSelectedRowHeight
            : 0f;
        var maxFromWindow = position.height - 180f - GetSectionChromeHeight() - selectedMin;
        return Mathf.Clamp(maxFromWindow, MinStatusConsoleHeight, MaxStatusConsoleHeight);
    }

    float GetMaxSelectedRowHeight()
    {
        var maxFromWindow = position.height - 180f - GetSectionChromeHeight() - GetStatusSectionLayoutHeight();
        return Mathf.Clamp(maxFromWindow, MinSelectedRowHeight, MaxSelectedRowHeight);
    }

    float GetStatusSectionLayoutHeight()
    {
        return GetSectionChromeHeight() + _statusConsoleHeight;
    }

    float GetSelectedRowSectionLayoutHeight()
    {
        if (!ShouldDrawSelectedRowPane())
            return 0f;

        return GetSectionChromeHeight() + _selectedRowHeight;
    }

    void DrawWorkArea()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));

        _windowScroll = EditorGUILayout.BeginScrollView(
            _windowScroll,
            false,
            false,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));

        EditorGUILayout.LabelField("Unity Cloud Save Admin Query", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Editor-only admin tool for listing, searching, and deleting public Cloud Save player data. " +
            "Results are shown in a sortable multi-column TreeView.",
            MessageType.Info);

        DrawAccountSection();
        EditorGUILayout.Space(8f);
        DrawModuleDeploySection();
        EditorGUILayout.Space(8f);
        DrawIndexesSection();
        EditorGUILayout.Space(8f);
        DrawQuerySection();
        EditorGUILayout.Space(8f);
        DrawPlayerManagementSection();
        EditorGUILayout.Space(8f);
        DrawCloudSaveEntitiesSection();
        EditorGUILayout.Space(8f);
        DrawDeleteSection();

        EditorGUILayout.Space(8f);
        DrawResultsToolbarAndFilters();
        EditorGUILayout.Space(4f);

        EditorGUILayout.EndScrollView();

        DrawResultsTables();
        EditorGUILayout.EndVertical();
    }

    void SetStatusMessage(string message)
    {
        message = message?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(message))
            return;

        if (string.Equals(_lastStatusLogMessage, message, StringComparison.Ordinal))
            return;

        _lastStatusLogMessage = message;
        _statusLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

        const int maxLines = 1000;
        if (_statusLog.Count > maxLines)
            _statusLog.RemoveRange(0, _statusLog.Count - maxLines);

        _statusScrollToBottom = true;
    }

    void ClearStatusLog()
    {
        _statusLog.Clear();
        _lastStatusLogMessage = string.Empty;
        _statusScroll = Vector2.zero;
    }

    void DrawStatusConsole()
    {
        _statusConsoleHeight = DrawVerticalResizeHandle(
            _statusConsoleHeight,
            MinStatusConsoleHeight,
            GetMaxStatusConsoleHeight(),
            ref _draggingStatusSplitter,
            invert: true);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Clear Log", GUILayout.Width(72f)))
            ClearStatusLog();
        EditorGUILayout.EndHorizontal();

        EnsureStatusConsoleStyle();

        var content = _statusLog.Count == 0 ? "Ready." : string.Join("\n", _statusLog);
        var viewWidth = Mathf.Max(100f, position.width - 40f);
        var contentHeight = _statusConsoleStyle.CalcHeight(new GUIContent(content), viewWidth);

        _statusScroll = EditorGUILayout.BeginScrollView(
            _statusScroll,
            false,
            false,
            GUILayout.Height(_statusConsoleHeight),
            GUILayout.ExpandWidth(true));

        EditorGUILayout.SelectableLabel(
            content,
            _statusConsoleStyle,
            GUILayout.MinHeight(Mathf.Max(_statusConsoleHeight - 8f, contentHeight)),
            GUILayout.ExpandWidth(true));

        EditorGUILayout.EndScrollView();

        if (_statusScrollToBottom && Event.current.type == EventType.Repaint)
        {
            _statusScroll.y = Mathf.Max(0f, contentHeight - _statusConsoleHeight + 8f);
            _statusScrollToBottom = false;
        }
    }

    void EnsureStatusConsoleStyle()
    {
        if (_statusConsoleStyle != null)
            return;

        _statusConsoleStyle = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true,
            richText = false,
            padding = new RectOffset(8, 8, 6, 6),
            stretchHeight = true
        };
        _statusConsoleStyle.normal.textColor = new Color(0.78f, 0.9f, 0.78f);
        _statusConsoleStyle.normal.background = GetStatusConsoleBackground();
        _statusConsoleStyle.focused = _statusConsoleStyle.normal;
        _statusConsoleStyle.hover = _statusConsoleStyle.normal;
        _statusConsoleStyle.active = _statusConsoleStyle.normal;

        var monoFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Consolas", "Courier New", "Menlo", "monospace" },
            12);
        if (monoFont != null)
            _statusConsoleStyle.font = monoFont;
    }

    static Texture2D GetStatusConsoleBackground()
    {
        if (_statusConsoleBackground != null)
            return _statusConsoleBackground;

        _statusConsoleBackground = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        _statusConsoleBackground.SetPixel(0, 0, new Color(0.11f, 0.11f, 0.11f));
        _statusConsoleBackground.Apply();
        return _statusConsoleBackground;
    }

    static Color GetAccountListSelectionColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.48f, 0.9f, 0.35f)
            : new Color(0.24f, 0.49f, 0.91f, 0.28f);
    }

    float DrawVerticalResizeHandle(float height, float min, float max, ref bool dragging, bool invert = false)
    {
        var handleRect = GUILayoutUtility.GetRect(
            0f,
            ResizeHandleHeight,
            GUILayout.ExpandWidth(true));
        var lineRect = new Rect(handleRect.x, handleRect.y + (handleRect.height * 0.5f) - 1f, handleRect.width, 2f);
        EditorGUI.DrawRect(lineRect, EditorGUIUtility.isProSkin
            ? new Color(0.55f, 0.55f, 0.55f, 0.7f)
            : new Color(0.35f, 0.35f, 0.35f, 0.55f));
        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeVertical);

        var e = Event.current;
        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && handleRect.Contains(e.mousePosition))
                {
                    dragging = true;
                    e.Use();
                }

                break;
            case EventType.MouseDrag:
                if (dragging)
                {
                    var delta = invert ? -e.delta.y : e.delta.y;
                    height = Mathf.Clamp(height + delta, min, max);
                    e.Use();
                    Repaint();
                }

                break;
            case EventType.MouseUp:
            case EventType.Ignore:
                if (dragging && (e.type == EventType.Ignore || e.button == 0))
                {
                    dragging = false;
                    if (e.type != EventType.Ignore)
                        e.Use();
                }

                break;
        }

        return height;
    }

    void DrawAccountSection()
    {
        EditorGUILayout.LabelField("Accounts", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Credentials are saved to " + CloudSaveAdminAccountsStore.FileName + " at the project root. " +
            "Each account stores a title, Key ID, Secret Key ID, Project ID, Environment ID, Adjust Token, and Adjust S2S Token.",
            MessageType.None);

        EnsureAccountsLoaded();
        DrawAccountToolbar();

        EditorGUI.BeginChangeCheck();
        _accountTitle = EditorGUILayout.TextField("Title", _accountTitle);
        _serviceKeyId = EditorGUILayout.TextField("Key ID", _serviceKeyId);
        _serviceSecretKey = EditorGUILayout.TextField("Secret Key ID", _serviceSecretKey);

        EditorGUILayout.BeginHorizontal();
        _projectId = EditorGUILayout.TextField("Project ID", _projectId);
        if (GUILayout.Button("From linked", GUILayout.Width(90f)))
        {
            _projectId = ReadCloudProjectId();
            PersistSelectedAccount();
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        _environmentId = EditorGUILayout.TextField("Environment ID", _environmentId);
        if (GUILayout.Button("From linked", GUILayout.Width(90f)))
        {
            _environmentId = ReadEnvironmentId();
            PersistSelectedAccount();
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        _adjustToken = EditorGUILayout.TextField("Adjust Token", _adjustToken);
        _adjustS2sToken = EditorGUILayout.TextField("Adjust S2S Token", _adjustS2sToken);

        if (EditorGUI.EndChangeCheck())
            PersistSelectedAccount();

        EditorGUILayout.HelpBox(
            "Uses HTTP Basic auth (Key ID as username, Secret Key ID as password).\n" +
            "If you get 403 No Permissions: Unity Dashboard > Administration > Service Accounts > " +
            "your account > Manage project roles > add Cloud Save Editor (or Viewer) for this project.",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        _useBearerToken = EditorGUILayout.ToggleLeft("Use long-lived bearer token (from dashboard)", _useBearerToken);
        if (_useBearerToken)
        {
            _bearerToken = EditorGUILayout.TextField("Bearer Token", _bearerToken);
            EditorGUILayout.HelpBox(
                "Create under Organization > Service accounts > Bearer tokens. " +
                "Do not paste token-exchange accessToken values here. " +
                "Bearer token is still saved in EditorPrefs; Key ID / Secret / Project / Environment stay in the JSON account.",
                MessageType.None);
        }

        if (EditorGUI.EndChangeCheck())
            SaveAuthPrefs();
    }

    void DrawAccountToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        _accountFilter = EditorGUILayout.TextField("Filter", _accountFilter);
        var matchCount = CountAccountTitleMatches();
        EditorGUILayout.LabelField($"{matchCount}/{_accounts.Count}", GUILayout.Width(64f));
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_accountFilter));
        if (GUILayout.Button("Clear", GUILayout.Width(50f)))
        {
            _accountFilter = string.Empty;
            GUI.FocusControl(null);
        }

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        var filteredIndices = GetFilteredAccountIndices();
        var rowHeight = AccountListRowHeight;
        var visibleRows = Mathf.Clamp(filteredIndices.Count == 0 ? 1 : filteredIndices.Count, 1, AccountListVisibleRows);
        var listHeight = visibleRows * rowHeight + 6f;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _accountListScroll = EditorGUILayout.BeginScrollView(
            _accountListScroll,
            false,
            false,
            GUILayout.Height(listHeight),
            GUILayout.ExpandWidth(true));

        if (filteredIndices.Count == 0)
        {
            EditorGUILayout.LabelField("(no matches)", EditorStyles.miniLabel);
        }
        else
        {
            var labels = GetAccountPopupLabels(filteredIndices);
            var selectedFullIndex = GetSelectedAccountIndex();
            for (var i = 0; i < filteredIndices.Count; i++)
            {
                var fullIndex = filteredIndices[i];
                var isSelected = fullIndex == selectedFullIndex;
                var rowRect = EditorGUILayout.GetControlRect(false, rowHeight, GUILayout.ExpandWidth(true));
                if (isSelected)
                    EditorGUI.DrawRect(rowRect, GetAccountListSelectionColor());

                var accountId = _accounts[fullIndex].id ?? string.Empty;
                var toggleRect = new Rect(rowRect.x + 2f, rowRect.y, 18f, rowRect.height);
                var labelRect = new Rect(rowRect.x + 22f, rowRect.y, Mathf.Max(20f, rowRect.width - 24f), rowRect.height);
                var marked = _deployAccountIds.Contains(accountId);
                var nowMarked = GUI.Toggle(toggleRect, marked, GUIContent.none);
                if (nowMarked != marked)
                {
                    if (nowMarked)
                        _deployAccountIds.Add(accountId);
                    else
                        _deployAccountIds.Remove(accountId);
                }

                EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.Link);
                if (GUI.Button(labelRect, labels[i], EditorStyles.label) && !isSelected)
                    SelectAccountAt(fullIndex);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(_isBusy);
        if (GUILayout.Button("New", GUILayout.Width(50f)))
            CreateNewAccount();
        if (GUILayout.Button("Duplicate", GUILayout.Width(74f)))
            DuplicateSelectedAccount();

        EditorGUI.BeginDisabledGroup(_accounts.Count <= 1);
        if (GUILayout.Button("Delete", GUILayout.Width(60f)))
            DeleteSelectedAccount();
        EditorGUI.EndDisabledGroup();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Check visible", GUILayout.Width(100f)))
            CheckVisibleAccounts();
        if (GUILayout.Button("Uncheck all", GUILayout.Width(90f)))
            _deployAccountIds.Clear();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    void LoadAccountsFromJson()
    {
        var file = CloudSaveAdminAccountsStore.Load(
            _serviceKeyId,
            _serviceSecretKey,
            ReadCloudProjectId(),
            ReadEnvironmentId());

        _accounts.Clear();
        if (file.accounts != null)
        {
            for (var i = 0; i < file.accounts.Length; i++)
            {
                if (file.accounts[i] != null)
                    _accounts.Add(CloudSaveAdminAccountsStore.Clone(file.accounts[i]));
            }
        }

        EnsureDefaultAccount();
        _selectedAccountId = file.selectedId;
        ApplySelectedAccountToFields();
    }

    void EnsureAccountsLoaded()
    {
        if (_accounts.Count > 0)
            return;

        EnsureDefaultAccount();
        ApplySelectedAccountToFields();
    }

    void EnsureDefaultAccount()
    {
        if (_accounts.Count > 0)
            return;

        var account = CloudSaveAdminAccountsStore.CreateAccount(
            "Default",
            _serviceKeyId,
            _serviceSecretKey,
            string.IsNullOrWhiteSpace(_projectId) ? ReadCloudProjectId() : _projectId,
            string.IsNullOrWhiteSpace(_environmentId) ? ReadEnvironmentId() : _environmentId);
        _accounts.Add(account);
        _selectedAccountId = account.id;
    }

    int GetSelectedAccountIndex()
    {
        var index = _accounts.FindIndex(a => string.Equals(a.id, _selectedAccountId, StringComparison.Ordinal));
        return index >= 0 ? index : 0;
    }

    CloudSaveAdminAccount GetSelectedAccount()
    {
        if (_accounts.Count == 0)
            return null;

        var index = GetSelectedAccountIndex();
        return _accounts[index];
    }

    int CountAccountTitleMatches()
    {
        var filter = (_accountFilter ?? string.Empty).Trim();
        var count = 0;
        for (var i = 0; i < _accounts.Count; i++)
        {
            if (AccountTitleMatchesFilter(_accounts[i], filter))
                count++;
        }

        return count;
    }

    List<int> GetFilteredAccountIndices()
    {
        var filter = (_accountFilter ?? string.Empty).Trim();
        var indices = new List<int>(_accounts.Count);
        for (var i = 0; i < _accounts.Count; i++)
        {
            if (AccountTitleMatchesFilter(_accounts[i], filter))
                indices.Add(i);
        }

        var selectedIndex = GetSelectedAccountIndex();
        if (selectedIndex >= 0 && !indices.Contains(selectedIndex))
            indices.Insert(0, selectedIndex);

        return indices;
    }

    static bool AccountTitleMatchesFilter(CloudSaveAdminAccount account, string filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;

        var title = account?.title ?? string.Empty;
        return title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    string[] GetAccountPopupLabels(List<int> accountIndices)
    {
        var labels = new string[accountIndices.Count];
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < accountIndices.Count; i++)
        {
            var account = _accounts[accountIndices[i]];
            var title = string.IsNullOrWhiteSpace(account.title) ? "(untitled)" : account.title.Trim();
            if (used.TryGetValue(title, out var count))
            {
                count++;
                used[title] = count;
                labels[i] = $"{title} ({count})";
            }
            else
            {
                used[title] = 1;
                labels[i] = title;
            }
        }

        return labels;
    }

    void SelectAccountAt(int index)
    {
        if (index < 0 || index >= _accounts.Count)
            return;

        PersistSelectedAccount();
        _selectedAccountId = _accounts[index].id;
        ApplySelectedAccountToFields();
        PersistSelectedAccount();
        GUI.FocusControl(null);
        ScrollAccountListToAccount(index);
    }

    void ScrollAccountListToAccount(int fullIndex)
    {
        var filteredIndices = GetFilteredAccountIndices();
        var listIndex = filteredIndices.IndexOf(fullIndex);
        if (listIndex < 0)
            return;

        var viewHeight = Mathf.Clamp(filteredIndices.Count, 1, AccountListVisibleRows) * AccountListRowHeight;
        var y = listIndex * AccountListRowHeight;
        if (y < _accountListScroll.y)
            _accountListScroll.y = y;
        else if (y + AccountListRowHeight > _accountListScroll.y + viewHeight)
            _accountListScroll.y = Mathf.Max(0f, y + AccountListRowHeight - viewHeight);
    }

    void ApplySelectedAccountToFields()
    {
        var account = GetSelectedAccount();
        if (account == null)
            return;

        _selectedAccountId = account.id;
        _accountTitle = account.title ?? string.Empty;
        _serviceKeyId = account.keyId ?? string.Empty;
        _serviceSecretKey = account.secretKeyId ?? string.Empty;
        _projectId = account.projectId ?? string.Empty;
        _environmentId = account.environmentId ?? string.Empty;
        _adjustToken = account.adjustToken ?? string.Empty;
        _adjustS2sToken = account.adjustS2sToken ?? string.Empty;
    }

    void PersistSelectedAccount()
    {
        if (_accounts.Count == 0)
            return;

        var account = GetSelectedAccount();
        if (account == null)
            return;

        account.title = _accountTitle ?? string.Empty;
        account.keyId = _serviceKeyId ?? string.Empty;
        account.secretKeyId = _serviceSecretKey ?? string.Empty;
        account.projectId = _projectId ?? string.Empty;
        account.environmentId = _environmentId ?? string.Empty;
        account.adjustToken = _adjustToken ?? string.Empty;
        account.adjustS2sToken = _adjustS2sToken ?? string.Empty;
        CloudSaveAdminAccountsStore.Save(_selectedAccountId, _accounts);
    }

    void CreateNewAccount()
    {
        PersistSelectedAccount();
        var title = CloudSaveAdminAccountsStore.UniqueTitle(_accounts, "New Account");
        var account = CloudSaveAdminAccountsStore.CreateAccount(
            title,
            string.Empty,
            string.Empty,
            ReadCloudProjectId(),
            ReadEnvironmentId());
        _accounts.Add(account);
        _selectedAccountId = account.id;
        ApplySelectedAccountToFields();
        PersistSelectedAccount();
        GUI.FocusControl(null);
    }

    void DuplicateSelectedAccount()
    {
        PersistSelectedAccount();
        var selected = GetSelectedAccount();
        if (selected == null)
            return;

        var clone = CloudSaveAdminAccountsStore.Clone(selected);
        clone.id = Guid.NewGuid().ToString("N");
        clone.title = CloudSaveAdminAccountsStore.UniqueTitle(
            _accounts,
            string.IsNullOrWhiteSpace(selected.title) ? "New Account" : selected.title.Trim() + " copy");
        var insertAt = GetSelectedAccountIndex() + 1;
        _accounts.Insert(insertAt, clone);
        _selectedAccountId = clone.id;
        ApplySelectedAccountToFields();
        PersistSelectedAccount();
        GUI.FocusControl(null);
    }

    void DeleteSelectedAccount()
    {
        if (_accounts.Count <= 1)
            return;

        var selected = GetSelectedAccount();
        var title = string.IsNullOrWhiteSpace(selected?.title) ? "(untitled)" : selected.title.Trim();
        if (!EditorUtility.DisplayDialog(
                "Delete Account",
                $"Remove '{title}' from {CloudSaveAdminAccountsStore.FileName}?",
                "Delete",
                "Cancel"))
            return;

        var index = GetSelectedAccountIndex();
        var removedId = _accounts[index].id;
        _accounts.RemoveAt(index);
        _deployAccountIds.Remove(removedId);
        var nextIndex = Mathf.Clamp(index, 0, _accounts.Count - 1);
        _selectedAccountId = _accounts[nextIndex].id;
        ApplySelectedAccountToFields();
        PersistSelectedAccount();
        GUI.FocusControl(null);
    }

    void LoadAuthPrefs()
    {
        _serviceKeyId = GetProjectAuthPref("KeyId", PrefsPrefix + "KeyId");
        _serviceSecretKey = GetProjectAuthPref("SecretKey", PrefsPrefix + "SecretKey");
        _bearerToken = GetProjectAuthPref("BearerToken", PrefsPrefix + "BearerToken");

        var projectUseBearerKey = GetProjectPrefsKey("UseBearer");
        _useBearerToken = EditorPrefs.HasKey(projectUseBearerKey)
            ? EditorPrefs.GetBool(projectUseBearerKey, false)
            : EditorPrefs.GetBool(PrefsPrefix + "UseBearer", false);
    }

    void SaveAuthPrefs()
    {
        EditorPrefs.SetString(GetProjectPrefsKey("BearerToken"), _bearerToken ?? string.Empty);
        EditorPrefs.SetBool(GetProjectPrefsKey("UseBearer"), _useBearerToken);
    }

    static string GetProjectPrefsKey(string suffix)
    {
        var projectId = (Application.dataPath ?? string.Empty).Replace('\\', '/');
        return PrefsPrefix + projectId + "_" + suffix;
    }

    static string GetProjectAuthPref(string suffix, string legacyGlobalKey)
    {
        var projectKey = GetProjectPrefsKey(suffix);
        if (EditorPrefs.HasKey(projectKey))
            return EditorPrefs.GetString(projectKey, string.Empty);

        return EditorPrefs.GetString(legacyGlobalKey, string.Empty);
    }

    void CheckVisibleAccounts()
    {
        var filtered = GetFilteredAccountIndices();
        for (var i = 0; i < filtered.Count; i++)
        {
            var id = _accounts[filtered[i]].id;
            if (!string.IsNullOrEmpty(id))
                _deployAccountIds.Add(id);
        }
    }

    void DrawModuleDeploySection()
    {
        EditorGUILayout.LabelField("Cloud Code Module Deploy", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds Module/Project (linux-x64) and uploads it with Cloud Code Admin API Basic auth.\n" +
            "Each target account must have Key ID, Secret, Project ID, and Environment ID.\n" +
            "APP_TOKEN and ADJUST_S2S_TOKEN in Module.cs are patched per account when those fields are filled, then restored.\n" +
            "Requires Cloud Code Editor on the service account. Check accounts in the list to deploy to several projects.",
            MessageType.None);

        var checkedCount = CountCheckedDeployAccounts();
        EditorGUILayout.LabelField("Checked accounts", checkedCount.ToString());

        EditorGUI.BeginDisabledGroup(_isBusy);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Deploy to current account", GUILayout.Height(26f)))
            _ = DeployModuleAsync(false);
        EditorGUI.BeginDisabledGroup(checkedCount == 0);
        if (GUILayout.Button($"Deploy to checked ({checkedCount})", GUILayout.Height(26f)))
            _ = DeployModuleAsync(true);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
    }

    int CountCheckedDeployAccounts()
    {
        var count = 0;
        for (var i = 0; i < _accounts.Count; i++)
        {
            var id = _accounts[i].id;
            if (!string.IsNullOrEmpty(id) && _deployAccountIds.Contains(id))
                count++;
        }

        return count;
    }

    List<CloudSaveAdminAccount> GetDeployTargets(bool checkedOnly)
    {
        PersistSelectedAccount();
        var targets = new List<CloudSaveAdminAccount>();
        if (!checkedOnly)
        {
            var selected = GetSelectedAccount();
            if (selected != null)
                targets.Add(CloudSaveAdminAccountsStore.Clone(selected));
            return targets;
        }

        for (var i = 0; i < _accounts.Count; i++)
        {
            var account = _accounts[i];
            if (account != null && !string.IsNullOrEmpty(account.id) && _deployAccountIds.Contains(account.id))
                targets.Add(CloudSaveAdminAccountsStore.Clone(account));
        }

        return targets;
    }

    async Task DeployModuleAsync(bool checkedOnly)
    {
        PersistSelectedAccount();
        var targets = GetDeployTargets(checkedOnly);
        if (targets.Count == 0)
        {
            SetStatusMessage(checkedOnly ? "No checked accounts to deploy." : "No current account to deploy.");
            return;
        }

        var label = checkedOnly
            ? $"Deploy Cloud Code module to {targets.Count} checked account(s)?"
            : $"Deploy Cloud Code module to '{(string.IsNullOrWhiteSpace(targets[0].title) ? "current account" : targets[0].title)}'?";
        if (!EditorUtility.DisplayDialog("Deploy Module", label, "Deploy", "Cancel"))
            return;

        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var cancellationToken = _fetchCts.Token;

        _isBusy = true;
        string backup = null;
        var publishDir = Path.Combine(Path.GetTempPath(), "CloudCodeModulePublish-" + Guid.NewGuid().ToString("N"));
        var succeeded = 0;
        var failed = 0;

        try
        {
            backup = CloudCodeModulePackager.BackupModuleSource();
            Directory.CreateDirectory(publishDir);

            for (var i = 0; i < targets.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = targets[i];
                var title = string.IsNullOrWhiteSpace(account.title) ? "(untitled)" : account.title.Trim();
                var error = ValidateAccountForModuleDeploy(account);
                if (!string.IsNullOrEmpty(error))
                {
                    failed++;
                    SetStatusMessage($"Skip {title}: {error}");
                    Repaint();
                    continue;
                }

                try
                {
                    SetStatusMessage($"[{i + 1}/{targets.Count}] {title}: patching Adjust tokens...");
                    Repaint();
                    CloudCodeModulePackager.RestoreModuleSource(backup);
                    CloudCodeModulePackager.ApplyTokens(account.adjustToken, account.adjustS2sToken);

                    SetStatusMessage($"[{i + 1}/{targets.Count}] {title}: publishing linux-x64 module...");
                    Repaint();
                    await CloudCodeModulePackager.PublishLinuxAsync(publishDir, cancellationToken);

                    SetStatusMessage($"[{i + 1}/{targets.Count}] {title}: uploading to Cloud Code...");
                    Repaint();
                    var zipBytes = CloudCodeModulePackager.ZipPublishDirectory(publishDir);
                    var authorization = BuildBasicAuthorizationHeader(account.keyId, account.secretKeyId);
                    await UploadCloudCodeModuleAsync(
                        account.projectId.Trim(),
                        account.environmentId.Trim(),
                        authorization,
                        zipBytes,
                        cancellationToken);

                    succeeded++;
                    SetStatusMessage($"[{i + 1}/{targets.Count}] {title}: deployed.");
                    Repaint();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    SetStatusMessage($"[{i + 1}/{targets.Count}] {title}: failed — {TruncateStatus(ex.Message)}");
                    Debug.LogWarning($"[CloudCodeDeploy] {title}: {ex.Message}");
                    Repaint();
                }
            }

            SetStatusMessage($"Module deploy finished. Succeeded {succeeded}, failed {failed}.");
        }
        catch (OperationCanceledException)
        {
            SetStatusMessage("Module deploy cancelled.");
        }
        catch (Exception ex)
        {
            SetStatusMessage("Module deploy failed: " + TruncateStatus(ex.Message));
            Debug.LogException(ex);
        }
        finally
        {
            try
            {
                if (!string.IsNullOrEmpty(backup))
                    CloudCodeModulePackager.RestoreModuleSource(backup);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CloudCodeDeploy] Failed to restore Module.cs: " + ex.Message);
            }

            try
            {
                if (Directory.Exists(publishDir))
                    Directory.Delete(publishDir, true);
            }
            catch
            {
                // Ignore temp cleanup failures.
            }

            _fetchCts?.Dispose();
            _fetchCts = null;
            _isBusy = false;
            Repaint();
        }
    }

    static string ValidateAccountForModuleDeploy(CloudSaveAdminAccount account)
    {
        if (account == null)
            return "Account is missing.";
        if (string.IsNullOrWhiteSpace(account.keyId) || string.IsNullOrWhiteSpace(account.secretKeyId))
            return "Key ID and Secret Key ID are required.";
        if (string.IsNullOrWhiteSpace(account.projectId))
            return "Project ID is required.";
        if (string.IsNullOrWhiteSpace(account.environmentId))
            return "Environment ID is required.";
        return string.Empty;
    }

    static string BuildBasicAuthorizationHeader(string keyId, string secretKey)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{keyId.Trim()}:{secretKey}"));
        return $"Basic {credentials}";
    }

    async Task UploadCloudCodeModuleAsync(
        string projectId,
        string environmentId,
        string authorizationHeader,
        byte[] zipBytes,
        CancellationToken cancellationToken)
    {
        var exists = await CloudCodeModuleExistsAsync(projectId, environmentId, authorizationHeader, cancellationToken);
        var url = exists
            ? BuildCloudCodeModuleUrl(projectId, environmentId) + "/" + Uri.EscapeDataString(CloudCodeModulePackager.ModuleName)
            : BuildCloudCodeModuleUrl(projectId, environmentId);
        var method = exists ? "PATCH" : UnityWebRequest.kHttpVerbPOST;

        var form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", zipBytes, CloudCodeModulePackager.ModuleName + ".zip", "application/zip")
        };
        if (!exists)
        {
            form.Insert(0, new MultipartFormDataSection("name", CloudCodeModulePackager.ModuleName));
            form.Insert(1, new MultipartFormDataSection("language", "CS"));
            form.Insert(2, new MultipartFormDataSection("tags", "{}"));
        }
        else
        {
            form.Insert(0, new MultipartFormDataSection("tags", "{}"));
        }

        await SendMultipartAsync(url, method, form, authorizationHeader, cancellationToken);
    }

    async Task<bool> CloudCodeModuleExistsAsync(
        string projectId,
        string environmentId,
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        var url = BuildCloudCodeModuleUrl(projectId, environmentId)
                  + "/" + Uri.EscapeDataString(CloudCodeModulePackager.ModuleName);
        var response = await SendWithStatusAsync(
            url,
            UnityWebRequest.kHttpVerbGET,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            },
            cancellationToken);

        if (response.StatusCode == 200)
            return true;
        if (response.StatusCode == 404)
            return false;

        throw new InvalidOperationException(
            $"Failed to check Cloud Code module ({response.StatusCode}): {response.Body}");
    }

    static string BuildCloudCodeModuleUrl(string projectId, string environmentId)
    {
        return "https://services.api.unity.com/cloud-code/v1/projects/"
               + Uri.EscapeDataString(projectId)
               + "/environments/"
               + Uri.EscapeDataString(environmentId)
               + "/modules";
    }

    void DrawIndexesSection()
    {
        EditorGUILayout.LabelField("Indexes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates a public player index (asc) for each field on the production environment of the linked project:\n" +
            string.Join(", ", PublicIndexFields) + "\n" +
            "Also creates default custom item ignoreBlocks / valueBoolString = \"false\".\n" +
            "Also creates Cloud Save key-saved triggers: payload→Module/Payload, load→Module/Load.\n" +
            "Requires Cloud Save Editor + Triggers Configuration Editor roles.\n" +
            "Triggers Admin API needs Service Account Key ID + Secret (Basic auth). Existing indexes/triggers are skipped.",
            MessageType.None);

        EditorGUI.BeginDisabledGroup(_isBusy);
        if (GUILayout.Button("Create Production Indexes", GUILayout.Height(26f)))
            _ = CreateProductionIndexesAsync();
        EditorGUI.EndDisabledGroup();
    }

    void DrawQuerySection()
    {
        EditorGUILayout.LabelField("Query", EditorStyles.boldLabel);

        _queryMode = (QueryMode)EditorGUILayout.EnumPopup("Mode", _queryMode);

        if (_queryMode == QueryMode.Search)
        {
            EditorGUILayout.HelpBox(
                "Indexed fields use exact (EQ) query match. Multiple filters are combined with AND.\n" +
                "Check Missing to find items that do not have that field saved.\n" +
                "Indexed: " + string.Join(", ", PublicIndexFields),
                MessageType.None);

            DrawQueryField("ip", ref _ip, ref _ipMatchMissing);
            DrawQueryField("adId (Google advertising id)", ref _adId, ref _adIdMatchMissing);
            DrawQueryField("deviceID (Adjust ad id)", ref _deviceId, ref _deviceIdMatchMissing);
            DrawQueryField("androidId", ref _androidId, ref _androidIdMatchMissing);
            DrawQueryField("pushToken", ref _pushToken, ref _pushTokenMatchMissing);
            DrawQueryField("devModel", ref _devModel, ref _devModelMatchMissing);
            DrawQueryField("date_utc_server (exact)", ref _dateUtcServer, ref _dateUtcServerMatchMissing);
            EditorGUILayout.HelpBox(
                "Enter date as dd/MM/yyyy (e.g. 26/06/2026). Search sends MM/dd/yyyy to Cloud Save (e.g. 06/26/2026).",
                MessageType.None);
            DrawQueryField("playerId", ref _playerIdFilter, ref _playerIdMatchMissing);
            DrawQueryField("isPrivacy (true/false)", ref _isPrivacy, ref _isPrivacyMatchMissing);
            DrawQueryField("campaign", ref _campaign, ref _campaignMatchMissing);
            DrawQueryField("network", ref _network, ref _networkMatchMissing);
            DrawQueryField("hour (0-23)", ref _hour, ref _hourMatchMissing);
            DrawQueryField("month (1-12)", ref _month, ref _monthMatchMissing);
            DrawQueryField("day (1-31)", ref _day, ref _dayMatchMissing);
            _resultLimit = EditorGUILayout.IntSlider("Result Limit", _resultLimit, 1, 100);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Lists players with Cloud Save data in this environment, then loads public data for each. " +
                "Pagination continues until all players are loaded or Max Players is reached.",
                MessageType.None);
            _listPlayerLimit = EditorGUILayout.IntField("Max Players (0 = all)", _listPlayerLimit);
            if (_listPlayerLimit < 0)
                _listPlayerLimit = 0;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(_isBusy);
        var buttonLabel = _isBusy
            ? "Working..."
            : _queryMode == QueryMode.ListAllPlayers
                ? "List All Players"
                : "Search";
        if (GUILayout.Button(buttonLabel, GUILayout.Height(30f)))
            _ = RunQueryAsync();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!_isBusy);
        if (GUILayout.Button("Stop", GUILayout.Width(72f), GUILayout.Height(30f)))
            StopFetching();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    static void DrawQueryField(string label, ref string value, ref bool matchMissing)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(matchMissing);
        value = EditorGUILayout.TextField(label, value);
        EditorGUI.EndDisabledGroup();
        matchMissing = GUILayout.Toggle(
            matchMissing,
            new GUIContent("Missing", "When checked, find items that do not have this field saved."),
            GUILayout.Width(80f));
        EditorGUILayout.EndHorizontal();
    }

    void DrawPlayerManagementSection()
    {
        EditorGUILayout.LabelField("Player Management", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Fetches only the player amount from Authentication Player Management " +
            "(project users). Does not load player rows.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(_isBusy);
        if (GUILayout.Button("Fetch Player Amount", GUILayout.Height(26f)))
            _ = FetchPlayerManagementAmountAsync();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!_isBusy);
        if (GUILayout.Button("Stop", GUILayout.Width(72f), GUILayout.Height(26f)))
            StopFetching();
        EditorGUI.EndDisabledGroup();

        var amountLabel = _playerManagementAmount.HasValue
            ? _playerManagementAmount.Value.ToString("N0")
            : "—";
        EditorGUILayout.LabelField("Players", amountLabel, GUILayout.MinWidth(160f));
        EditorGUILayout.EndHorizontal();
    }

    void DrawCloudSaveEntitiesSection()
    {
        EditorGUILayout.LabelField("Cloud Save Entities", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Fetches only the amount of Cloud Save player entities in this environment " +
            "(players that have Cloud Save data). Does not load item rows.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(_isBusy);
        if (GUILayout.Button("Fetch Entity Amount", GUILayout.Height(26f)))
            _ = FetchCloudSaveEntityAmountAsync();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!_isBusy);
        if (GUILayout.Button("Stop", GUILayout.Width(72f), GUILayout.Height(26f)))
            StopFetching();
        EditorGUI.EndDisabledGroup();

        var amountLabel = _cloudSaveEntityAmount.HasValue
            ? _cloudSaveEntityAmount.Value.ToString("N0")
            : "—";
        EditorGUILayout.LabelField("Entities", amountLabel, GUILayout.MinWidth(160f));
        EditorGUILayout.EndHorizontal();
    }

    void DrawDeleteSection()
    {
        EditorGUILayout.LabelField("Delete", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Completely deletes the player: Cloud Save (public/protected/default), Leaderboards scores, and Authentication account.\n" +
            "This cannot be undone.",
            MessageType.Warning);

        _deletePlayerId = EditorGUILayout.TextField("Player ID", _deletePlayerId);

        EditorGUI.BeginDisabledGroup(_isBusy || string.IsNullOrWhiteSpace(_deletePlayerId));
        if (GUILayout.Button("Delete Player Completely", GUILayout.Height(26f)))
            _ = DeletePlayerAsync();
        EditorGUI.EndDisabledGroup();
    }

    void DrawResultsToolbarAndFilters()
    {
        EditorGUILayout.BeginHorizontal();
        var visibleCount = _usersTreeView != null ? _usersTreeView.GetVisibleRowCount() : _resultRows.Count;
        var hasLocalFilter = HasActiveLocalFilter();
        var resultsLabel = !hasLocalFilter
            ? $"Results ({_resultRows.Count})"
            : $"Results ({visibleCount} / {_resultRows.Count})";
        EditorGUILayout.LabelField(resultsLabel, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        EditorGUI.BeginDisabledGroup(_resultRows.Count == 0);
        if (GUILayout.Button("Copy All", GUILayout.Width(70f)))
            CopyVisibleRowsAsTable();
        if (GUILayout.Button("Export Excel", GUILayout.Width(95f)))
            ExportResultsToExcel();
        EditorGUI.EndDisabledGroup();

        var hasSelectedRow = _usersTreeView?.GetSelectedRow() != null;
        EditorGUI.BeginDisabledGroup(!hasSelectedRow);
        if (GUILayout.Button("Copy Selected", GUILayout.Width(100f)))
            CopySelectedRowAsTable();
        if (GUILayout.Button("Use Selected", GUILayout.Width(90f)))
            ApplySelectedRowToDeleteField();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(_resultRows.Count == 0);
        if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            ClearResults();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        DrawLocalFilters();
        DrawMetadataReport();
    }

    void DrawResultsTables()
    {
        if (_usersTreeView != null && _resultRows.Count > 0)
        {
            var maxTreeHeight = Mathf.Max(
                MinTreeViewHeight,
                position.height - GetStatusSectionLayoutHeight() - GetSelectedRowSectionLayoutHeight() - 180f);
            _treeViewHeight = EditorGUILayout.Slider(
                "Table Height",
                Mathf.Clamp(_treeViewHeight, MinTreeViewHeight, maxTreeHeight),
                MinTreeViewHeight,
                maxTreeHeight);

            var rect = EditorGUILayout.GetControlRect(false, _treeViewHeight, GUILayout.ExpandWidth(true));
            _usersTreeView.OnGUI(rect);
            _treeViewHeight = DrawVerticalResizeHandle(
                _treeViewHeight,
                MinTreeViewHeight,
                maxTreeHeight,
                ref _draggingTreeSplitter);
        }
        else if (_isBusy && _queryMode == QueryMode.ListAllPlayers)
        {
            EditorGUILayout.HelpBox("Loading players... rows appear as each player is fetched.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                _resultRows.Count == 0
                    ? "No results yet. Run Search or List All Players."
                    : "Preparing results view...",
                MessageType.None);
        }
    }

    void DrawLocalFilters()
    {
        EnsureFieldFiltersForColumns();

        EditorGUI.BeginChangeCheck();
        _treeViewFilter = EditorGUILayout.TextField("Filter all fields", _treeViewFilter);
        if (EditorGUI.EndChangeCheck())
            ApplyLocalFiltersToTreeView();

        EditorGUILayout.HelpBox(
            "All-fields: any column contains this text (case-insensitive).\n" +
            "Per-field: each active field must match (AND). Empty checkbox matches blank values; unchecked ignores empty.",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();
        _fieldFiltersFoldout = EditorGUILayout.Foldout(_fieldFiltersFoldout, "Per-field filters", true);
        GUILayout.FlexibleSpace();
        EditorGUI.BeginDisabledGroup(!HasActiveLocalFilter());
        if (GUILayout.Button("Clear filters", GUILayout.Width(100f)))
            ClearLocalFilters();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (!_fieldFiltersFoldout)
            return;

        var filterKeys = GetFieldFilterKeys();
        if (filterKeys.Count == 0)
        {
            EditorGUILayout.HelpBox("No result columns yet. Run a query first.", MessageType.None);
            return;
        }

        var rowHeight = EditorGUIUtility.singleLineHeight + 4f;
        EditorGUI.BeginChangeCheck();
        for (var i = 0; i < filterKeys.Count; i++)
        {
            var key = filterKeys[i];
            var filter = _fieldFilters[key];

            EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
            EditorGUILayout.LabelField(key, GUILayout.Width(140f));

            EditorGUI.BeginDisabledGroup(filter.MatchEmpty);
            filter.Text = EditorGUILayout.TextField(filter.Text);
            EditorGUI.EndDisabledGroup();

            filter.MatchEmpty = GUILayout.Toggle(
                filter.MatchEmpty,
                new GUIContent("Empty", "When checked, show only rows where this field is empty. Unchecked ignores empty matching."),
                GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        if (EditorGUI.EndChangeCheck())
            ApplyLocalFiltersToTreeView();
    }

    void EnsureFieldFiltersForColumns()
    {
        if (_columnKeys == null)
            return;

        for (var i = 0; i < _columnKeys.Count; i++)
        {
            var key = _columnKeys[i];
            if (string.IsNullOrEmpty(key) || key == CloudSaveUsersTreeView.DeleteColumnKey)
                continue;

            if (!_fieldFilters.ContainsKey(key))
                _fieldFilters[key] = new CloudSaveFieldFilter();
        }
    }

    List<string> GetFieldFilterKeys()
    {
        var keys = new List<string>();
        if (_columnKeys == null)
            return keys;

        for (var i = 0; i < _columnKeys.Count; i++)
        {
            var key = _columnKeys[i];
            if (string.IsNullOrEmpty(key) || key == CloudSaveUsersTreeView.DeleteColumnKey)
                continue;

            keys.Add(key);
        }

        return keys;
    }

    bool HasActiveLocalFilter()
    {
        if (!string.IsNullOrEmpty(_treeViewFilter))
            return true;

        foreach (var pair in _fieldFilters)
        {
            if (pair.Value != null && pair.Value.IsActive)
                return true;
        }

        return false;
    }

    void ClearLocalFilters()
    {
        _treeViewFilter = string.Empty;
        foreach (var pair in _fieldFilters)
            pair.Value?.Clear();

        ApplyLocalFiltersToTreeView();
    }

    void ApplyLocalFiltersToTreeView()
    {
        if (_usersTreeView == null)
            return;

        _usersTreeView.SetFilters(_treeViewFilter, _fieldFilters);
        Repaint();
    }

    void DrawMetadataReport()
    {
        if (_resultRows.Count == 0)
            return;

        var rows = GetRowsForExport();
        var counts = CountMetadataTypes(rows);
        if (counts.Count == 0)
            return;

        var scoped = HasActiveLocalFilter();
        var header = scoped
            ? $"Metadata report ({rows.Count} filtered / {_resultRows.Count} loaded)"
            : $"Metadata report ({rows.Count} row(s))";

        var sb = new StringBuilder(header.Length + counts.Count * 40);
        sb.Append(header);
        for (var i = 0; i < counts.Count; i++)
        {
            var entry = counts[i];
            sb.Append('\n').Append(entry.Key).Append(": ").Append(entry.Value.ToString("N0"));
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox(sb.ToString(), MessageType.None);
        if (GUILayout.Button("Copy", GUILayout.Width(50f), GUILayout.ExpandHeight(true)))
        {
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            SetStatusMessage("Metadata report copied.");
        }

        EditorGUILayout.EndHorizontal();
    }

    static List<KeyValuePair<string, int>> CountMetadataTypes(List<Dictionary<string, string>> rows)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        if (rows == null)
            return new List<KeyValuePair<string, int>>();

        for (var i = 0; i < rows.Count; i++)
        {
            var raw = GetRowValue(rows[i], "metadata");
            var label = string.IsNullOrWhiteSpace(raw) ? "(empty)" : raw.Trim();
            totals.TryGetValue(label, out var count);
            totals[label] = count + 1;
        }

        var ordered = new List<KeyValuePair<string, int>>(totals.Count);
        foreach (var pair in totals)
            ordered.Add(pair);

        ordered.Sort((left, right) =>
        {
            var byCount = right.Value.CompareTo(left.Value);
            return byCount != 0
                ? byCount
                : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        });

        return ordered;
    }

    void ApplySelectedRowToDeleteField()
    {
        var selected = _usersTreeView?.GetSelectedRow();
        if (selected == null)
            return;

        if (selected.TryGetValue("playerId", out var playerId) && !string.IsNullOrWhiteSpace(playerId))
            _deletePlayerId = playerId;
    }

    void CopySelectedRowAsTable()
    {
        var selected = _usersTreeView?.GetSelectedRow();
        if (selected == null)
            return;

        var columns = GetExportColumns();
        EditorGUIUtility.systemCopyBuffer = FormatRowsAsTsv(
            new List<Dictionary<string, string>> { selected },
            columns);
        SetStatusMessage("Selected row copied as table.");
    }

    void CopyVisibleRowsAsTable()
    {
        var rows = GetRowsForExport();
        if (rows.Count == 0)
        {
            SetStatusMessage("No filtered results to copy.");
            return;
        }

        EditorGUIUtility.systemCopyBuffer = FormatRowsAsTsv(rows, GetExportColumns());
        SetStatusMessage(!HasActiveLocalFilter()
            ? $"Copied {rows.Count} row(s) as table."
            : $"Copied {rows.Count} filtered row(s) as table.");
    }

    List<Dictionary<string, string>> GetRowsForExport()
    {
        if (_usersTreeView != null)
            return _usersTreeView.GetVisibleRows();

        return new List<Dictionary<string, string>>(_resultRows);
    }

    List<string> GetExportColumns()
    {
        if (_usersTreeView != null)
        {
            var visible = _usersTreeView.GetVisibleExportColumns();
            if (visible.Count > 0)
                return visible;
        }

        return _columnKeys
            .Where(key => key != CloudSaveUsersTreeView.DeleteColumnKey)
            .ToList();
    }

    void DrawSelectedRowDetails()
    {
        _selectedRowHeight = DrawVerticalResizeHandle(
            _selectedRowHeight,
            MinSelectedRowHeight,
            GetMaxSelectedRowHeight(),
            ref _draggingSelectedSplitter,
            invert: true);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Selected Row", EditorStyles.boldLabel, GUILayout.Width(100f));
        _selectedRowHeight = EditorGUILayout.Slider(
            _selectedRowHeight,
            MinSelectedRowHeight,
            GetMaxSelectedRowHeight());
        var selectedCount = _usersTreeView != null ? _usersTreeView.GetSelectedRowCount() : 0;
        EditorGUILayout.LabelField($"Selected: {selectedCount}", EditorStyles.miniLabel, GUILayout.Width(110f));
        EditorGUILayout.EndHorizontal();

        var selected = _usersTreeView?.GetSelectedRow();
        if (selected == null || selected.Count == 0)
        {
            _selectedRowDetailsPlayerId = string.Empty;
            _selectedRowTreeView?.SetEntries(null);

            _selectedRowScroll = EditorGUILayout.BeginScrollView(
                _selectedRowScroll,
                false,
                false,
                GUILayout.Height(_selectedRowHeight),
                GUILayout.ExpandWidth(true));
            EditorGUILayout.HelpBox("Select a result row to inspect its fields.", MessageType.None);
            EditorGUILayout.EndScrollView();
            return;
        }

        EnsureSelectedRowTreeView(selected);

        var rect = EditorGUILayout.GetControlRect(false, _selectedRowHeight, GUILayout.ExpandWidth(true));
        _selectedRowTreeView.OnGUI(rect);
    }

    void EnsureSelectedRowTreeView(Dictionary<string, string> selected)
    {
        if (_selectedRowTreeView == null)
            RebuildSelectedRowTreeView();

        var selection = _usersTreeView?.GetSelection();
        var selectedId = selection != null && selection.Count > 0 ? selection[0] : -1;
        var playerId = GetRowValue(selected, "playerId");
        var cacheKey = selectedId + ":" + playerId + ":" + selected.Count;
        if (string.Equals(_selectedRowDetailsPlayerId, cacheKey, StringComparison.Ordinal))
            return;

        _selectedRowDetailsPlayerId = cacheKey;
        _selectedRowTreeView.SetEntries(BuildSelectedRowEntries(selected));
    }

    void RebuildSelectedRowTreeView()
    {
        _selectedRowTreeState ??= new TreeViewState();

        var headerState = CloudSaveSelectedRowTreeView.CreateDefaultHeaderState();
        if (_selectedRowHeaderState != null)
            MultiColumnHeaderState.OverwriteSerializedFields(_selectedRowHeaderState, headerState);
        else
            _selectedRowHeaderState = headerState;

        var header = new MultiColumnHeader(_selectedRowHeaderState);
        header.ResizeToFit();
        _selectedRowTreeView = new CloudSaveSelectedRowTreeView(_selectedRowTreeState, header);
    }

    List<CloudSaveSelectedRowTreeView.DetailEntry> BuildSelectedRowEntries(Dictionary<string, string> selected)
    {
        var entries = new List<CloudSaveSelectedRowTreeView.DetailEntry>();
        if (selected == null)
            return entries;

        var detailKeys = _columnKeys
            .Where(key => key != CloudSaveUsersTreeView.DeleteColumnKey)
            .ToList();

        foreach (var key in detailKeys)
        {
            if (!selected.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                continue;

            entries.Add(new CloudSaveSelectedRowTreeView.DetailEntry(
                key,
                CloudSaveTimeFormat.FormatFieldForDisplay(key, value)));
        }

        foreach (var pair in selected)
        {
            if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                continue;
            if (detailKeys.Contains(pair.Key))
                continue;

            entries.Add(new CloudSaveSelectedRowTreeView.DetailEntry(
                pair.Key,
                CloudSaveTimeFormat.FormatFieldForDisplay(pair.Key, pair.Value)));
        }

        return entries;
    }

    void ClearResults()
    {
        _resultRows.Clear();
        _treeViewFilter = string.Empty;
        foreach (var pair in _fieldFilters)
            pair.Value?.Clear();
        _selectedRowDetailsPlayerId = string.Empty;
        _selectedRowTreeView?.SetEntries(null);
        _columnKeys = BuildColumnKeys(_resultRows);
        RebuildTreeView();
        Repaint();
    }

    void SetResults(List<Dictionary<string, string>> rows)
    {
        _resultRows.Clear();
        if (rows != null)
        {
            foreach (var row in rows)
            {
                CloudSaveTimeFormat.NormalizeRow(row);
                _resultRows.Add(row);
            }
        }

        _columnKeys = BuildColumnKeys(_resultRows);
        RebuildTreeView();
    }

    void InitializeStreamingResults()
    {
        _resultRows.Clear();
        _columnKeys = BuildColumnKeys(_resultRows);
        RebuildTreeView();
        Repaint();
    }

    void AppendResultRow(Dictionary<string, string> row)
    {
        if (row == null)
            return;

        CloudSaveTimeFormat.NormalizeRow(row);
        _resultRows.Add(row);

        var updatedColumns = BuildColumnKeys(_resultRows);
        if (!ColumnsEqual(_columnKeys, updatedColumns))
        {
            _columnKeys = updatedColumns;
            RebuildTreeView();
        }
        else
        {
            _usersTreeView?.ReloadRows();
        }

        Repaint();
    }

    static bool ColumnsEqual(List<string> left, List<string> right)
    {
        if (left == null || right == null)
            return left == right;

        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    void RebuildTreeView()
    {
        _treeViewState ??= new TreeViewState();

        var headerState = CloudSaveUsersTreeView.CreateDefaultHeaderState(_columnKeys);
        if (_treeHeaderState != null && HeaderMatchesColumnKeys(_treeHeaderState, _columnKeys))
            MultiColumnHeaderState.OverwriteSerializedFields(_treeHeaderState, headerState);
        else
            _treeHeaderState = headerState;

        var header = new MultiColumnHeader(_treeHeaderState);
        header.ResizeToFit();
        _usersTreeView = new CloudSaveUsersTreeView(
            _treeViewState,
            header,
            _columnKeys,
            _resultRows,
            playerId => _ = DeletePlayerAsync(playerId),
            () => !_isBusy);
        EnsureFieldFiltersForColumns();
        ApplyLocalFiltersToTreeView();
    }

    static bool HeaderMatchesColumnKeys(MultiColumnHeaderState headerState, List<string> columnKeys)
    {
        if (headerState?.columns == null || columnKeys == null)
            return false;

        if (headerState.columns.Length != columnKeys.Count)
            return false;

        for (var i = 0; i < columnKeys.Count; i++)
        {
            var expectedHeader = columnKeys[i] == CloudSaveUsersTreeView.DeleteColumnKey
                ? "Delete"
                : columnKeys[i];

            if (headerState.columns[i].headerContent?.text != expectedHeader)
                return false;
        }

        return true;
    }

    static List<string> BuildColumnKeys(List<Dictionary<string, string>> rows)
    {
        var columns = new List<string>();

        foreach (var key in PreferredColumnOrder)
        {
            if (!columns.Contains(key))
                columns.Add(key);
        }

        foreach (var key in ReturnKeys)
        {
            if (!columns.Contains(key))
                columns.Add(key);
        }

        foreach (var row in rows)
        {
            foreach (var key in row.Keys)
            {
                if (!columns.Contains(key))
                    columns.Add(key);
            }
        }

        if (!columns.Contains(CloudSaveUsersTreeView.DeleteColumnKey))
            columns.Add(CloudSaveUsersTreeView.DeleteColumnKey);

        EnsureTrailingColumnOrder(columns);
        return columns;
    }

    static void EnsureTrailingColumnOrder(List<string> columns)
    {
        var hasNaming = columns.Remove("naming");
        var hasError = columns.Remove("error");
        columns.Remove(CloudSaveUsersTreeView.DeleteColumnKey);

        if (hasNaming)
            columns.Add("naming");
        if (hasError)
            columns.Add("error");

        columns.Add(CloudSaveUsersTreeView.DeleteColumnKey);
    }

    static string FormatRowsAsTsv(List<Dictionary<string, string>> rows, List<string> columns)
    {
        if (rows == null || rows.Count == 0 || columns == null || columns.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", columns));

        foreach (var row in rows)
        {
            var values = columns.Select(column =>
            {
                var raw = GetRowValue(row, column);
                var display = CloudSaveTimeFormat.FormatFieldForDisplay(column, raw);
                return display.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            });
            sb.AppendLine(string.Join("\t", values));
        }

        return sb.ToString();
    }

    void ExportResultsToExcel()
    {
        var rows = GetRowsForExport();
        if (rows.Count == 0)
        {
            SetStatusMessage(!HasActiveLocalFilter()
                ? "No results to export."
                : "No filtered results to export.");
            return;
        }

        var columns = GetExportColumns();
        if (columns.Count == 0)
        {
            SetStatusMessage("No TreeView columns to export.");
            return;
        }

        var defaultName = $"cloud-save-results-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        var path = EditorUtility.SaveFilePanel(
            "Export filtered results to Excel table",
            string.Empty,
            defaultName,
            "xlsx");

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            CloudSaveExcelTableExporter.WriteTable(
                path,
                columns,
                rows,
                (row, column) =>
                    CloudSaveTimeFormat.FormatFieldForDisplay(column, GetRowValue(row, column)));

            SetStatusMessage(!HasActiveLocalFilter()
                ? $"Exported {rows.Count} row(s) as Excel table to {path}"
                : $"Exported {rows.Count} filtered row(s) as Excel table to {path}");
            EditorUtility.RevealInFinder(path);
        }
        catch (Exception ex)
        {
            SetStatusMessage("Excel export failed.");
            Debug.LogException(ex);
        }
    }

    void StopFetching()
    {
        _fetchCts?.Cancel();
    }

    async Task CreateProductionIndexesAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var cancellationToken = _fetchCts.Token;

        _isBusy = true;
        SetStatusMessage("Resolving production environment...");
        Repaint();

        try
        {
            if (!ValidateAuthInputsForIndexes())
                return;

            if (string.IsNullOrWhiteSpace(_projectId))
                _projectId = ReadCloudProjectId();
            if (string.IsNullOrWhiteSpace(_projectId))
            {
                SetStatusMessage("Project ID is required. Fill it on the selected account, or link a Unity Cloud project.");
                return;
            }

            var authorizationHeader = BuildAuthorizationHeader();
            var productionEnvironmentId = await ResolveProductionEnvironmentIdAsync(
                authorizationHeader,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(productionEnvironmentId))
            {
                SetStatusMessage("Could not resolve production environment for this project.");
                return;
            }

            _environmentId = productionEnvironmentId;
            PersistSelectedAccount();

            var existingKeys = await ListExistingPublicPlayerIndexKeysAsync(
                authorizationHeader,
                productionEnvironmentId,
                cancellationToken);

            var created = 0;
            var skipped = 0;
            var failed = new List<string>();

            foreach (var field in PublicIndexFields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (existingKeys.Contains(field))
                {
                    skipped++;
                    SetStatusMessage($"Skipping existing index: {field}");
                    Repaint();
                    continue;
                }

                SetStatusMessage($"Creating index: {field}...");
                Repaint();

                try
                {
                    await CreatePublicPlayerIndexAsync(
                        authorizationHeader,
                        productionEnvironmentId,
                        field,
                        cancellationToken);
                    created++;
                    existingKeys.Add(field);
                }
                catch (Exception ex) when (IsHttpConflict(ex))
                {
                    skipped++;
                    existingKeys.Add(field);
                }
                catch (Exception ex)
                {
                    failed.Add($"{field}: {TruncateStatus(ex.Message)}");
                    Debug.LogWarning($"[CloudSaveAdminQuery] Index create failed for '{field}': {ex.Message}");
                }
            }

            var sb = new StringBuilder();
            sb.Append($"Production indexes ({productionEnvironmentId}): {created} created, {skipped} skipped");
            if (failed.Count > 0)
                sb.Append($", {failed.Count} failed — ").Append(string.Join(" | ", failed));
            else
                sb.Append('.');

            SetStatusMessage("Creating custom item ignoreBlocks...");
            Repaint();

            try
            {
                await SetDefaultCustomItemAsync(
                    authorizationHeader,
                    productionEnvironmentId,
                    "ignoreBlocks",
                    "valueBoolString",
                    "false",
                    cancellationToken);
                sb.Append(" Custom item ignoreBlocks/valueBoolString=\"false\" set.");
            }
            catch (Exception ex)
            {
                sb.Append(" Custom item failed — ").Append(TruncateStatus(ex.Message));
                Debug.LogWarning($"[CloudSaveAdminQuery] Custom item set failed: {ex.Message}");
            }

            SetStatusMessage("Creating Cloud Save triggers...");
            Repaint();

            var triggersAuth = BuildTriggersAuthorizationHeader(out var triggersAuthError);
            if (string.IsNullOrEmpty(triggersAuth))
            {
                sb.Append(" Triggers skipped — ").Append(triggersAuthError ?? "missing auth.");
                Debug.LogWarning("[CloudSaveAdminQuery] " + (triggersAuthError ?? "Triggers auth missing."));
            }
            else
            {
                var triggerCreated = 0;
                var triggerSkipped = 0;
                var triggerFailed = new List<string>();
                var existingTriggerNames = await ListExistingTriggerNamesAsync(
                    triggersAuth,
                    productionEnvironmentId,
                    cancellationToken);

                foreach (var trigger in CloudSaveTriggers)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (existingTriggerNames.Contains(trigger.Name))
                    {
                        triggerSkipped++;
                        SetStatusMessage($"Skipping existing trigger: {trigger.Name}");
                        Repaint();
                        continue;
                    }

                    SetStatusMessage($"Creating trigger: {trigger.Name}...");
                    Repaint();

                    try
                    {
                        await CreateCloudSaveTriggerAsync(
                            triggersAuth,
                            productionEnvironmentId,
                            trigger,
                            cancellationToken);
                        triggerCreated++;
                        existingTriggerNames.Add(trigger.Name);
                    }
                    catch (Exception ex) when (IsHttpConflict(ex))
                    {
                        triggerSkipped++;
                        existingTriggerNames.Add(trigger.Name);
                    }
                    catch (Exception ex)
                    {
                        var shortError = FormatApiError(ex.Message);
                        triggerFailed.Add($"{trigger.Name}: {shortError}");
                        Debug.LogError($"[CloudSaveAdminQuery] Trigger create failed for '{trigger.Name}':\n{ex.Message}");
                    }
                }

                sb.Append($" Triggers: {triggerCreated} created, {triggerSkipped} skipped");
                if (triggerFailed.Count > 0)
                    sb.Append($", {triggerFailed.Count} failed — ").Append(string.Join(" | ", triggerFailed));
                else
                    sb.Append('.');
            }

            SetStatusMessage(sb.ToString());
        }
        catch (OperationCanceledException)
        {
            SetStatusMessage("Index creation stopped.");
        }
        catch (Exception ex)
        {
            SetStatusMessage("Index creation failed.");
            Debug.LogException(ex);
        }
        finally
        {
            _fetchCts?.Dispose();
            _fetchCts = null;
            _isBusy = false;
            Repaint();
        }
    }

    bool ValidateAuthInputsForIndexes()
    {
        if (string.IsNullOrWhiteSpace(_projectId) && string.IsNullOrWhiteSpace(ReadCloudProjectId()))
        {
            SetStatusMessage("Project ID is required.");
            return false;
        }

        if (_useBearerToken)
        {
            if (string.IsNullOrWhiteSpace(_bearerToken))
            {
                SetStatusMessage("Bearer token is required.");
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(_serviceKeyId) || string.IsNullOrWhiteSpace(_serviceSecretKey))
        {
            SetStatusMessage("Service account key ID and secret are required.");
            return false;
        }

        return true;
    }

    async Task<string> ResolveProductionEnvironmentIdAsync(
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = "https://services.api.unity.com/unity/v1/projects/"
                      + Uri.EscapeDataString(_projectId.Trim())
                      + "/environments";

            var responseText = await SendAsync(
                url,
                UnityWebRequest.kHttpVerbGET,
                null,
                new Dictionary<string, string>
                {
                    { "Authorization", authorizationHeader },
                    { "Accept", "application/json" }
                },
                cancellationToken);

            if (TryPickProductionEnvironmentId(responseText, out var productionId))
                return productionId;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CloudSaveAdminQuery] List environments failed: {ex.Message}");
        }

        return _environmentId?.Trim() ?? string.Empty;
    }

    static bool TryPickProductionEnvironmentId(string rawJson, out string environmentId)
    {
        environmentId = string.Empty;
        if (string.IsNullOrEmpty(rawJson))
            return false;

        string fallbackDefaultId = null;
        var searchFrom = 0;

        while (true)
        {
            var nameIndex = rawJson.IndexOf("\"name\"", searchFrom, StringComparison.Ordinal);
            if (nameIndex < 0)
                break;

            var name = CloudSaveResponseParser.ReadJsonStringField(rawJson, "name", nameIndex);
            var idNearbyStart = Math.Max(0, nameIndex - 120);
            var idNearbyLength = Math.Min(rawJson.Length - idNearbyStart, 280);
            var nearby = rawJson.Substring(idNearbyStart, idNearbyLength);
            var id = CloudSaveResponseParser.ReadJsonStringField(nearby, "id", 0);

            if (!string.IsNullOrEmpty(id))
            {
                if (string.Equals(name, "production", StringComparison.OrdinalIgnoreCase))
                {
                    environmentId = id;
                    return true;
                }

                if (nearby.IndexOf("\"isDefault\":true", StringComparison.OrdinalIgnoreCase) >= 0
                    || nearby.IndexOf("\"isDefault\": true", StringComparison.OrdinalIgnoreCase) >= 0)
                    fallbackDefaultId = id;
            }

            searchFrom = nameIndex + 1;
        }

        if (!string.IsNullOrEmpty(fallbackDefaultId))
        {
            environmentId = fallbackDefaultId;
            return true;
        }

        return false;
    }

    async Task<HashSet<string>> ListExistingPublicPlayerIndexKeysAsync(
        string authorizationHeader,
        string environmentId,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var url = new StringBuilder()
            .Append(CloudSaveAdminBaseUrl)
            .Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(environmentId.Trim()))
            .Append("/indexes")
            .ToString();

        string responseText;
        try
        {
            responseText = await SendAsync(
                url,
                UnityWebRequest.kHttpVerbGET,
                null,
                new Dictionary<string, string>
                {
                    { "Authorization", authorizationHeader },
                    { "Accept", "application/json" }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CloudSaveAdminQuery] List indexes failed (will still try create): {ex.Message}");
            return keys;
        }

        CollectPublicPlayerIndexKeys(responseText, keys);
        return keys;
    }

    static void CollectPublicPlayerIndexKeys(string rawJson, HashSet<string> keys)
    {
        if (string.IsNullOrEmpty(rawJson) || keys == null)
            return;

        var searchFrom = 0;
        while (true)
        {
            var accessClassIndex = rawJson.IndexOf("\"accessClass\"", searchFrom, StringComparison.Ordinal);
            if (accessClassIndex < 0)
                break;

            var accessValue = CloudSaveResponseParser.ReadJsonStringField(rawJson, "accessClass", accessClassIndex);
            var nextAccess = rawJson.IndexOf("\"accessClass\"", accessClassIndex + 1, StringComparison.Ordinal);
            var segmentEnd = nextAccess >= 0 ? nextAccess : rawJson.Length;
            var segmentStart = Math.Max(0, accessClassIndex - 250);
            var segment = rawJson.Substring(segmentStart, segmentEnd - segmentStart);

            var entityType = CloudSaveResponseParser.ReadJsonStringField(segment, "entityType", 0);
            var isPublic = string.Equals(accessValue, "public", StringComparison.OrdinalIgnoreCase);
            var isPlayer = string.IsNullOrEmpty(entityType)
                           || string.Equals(entityType, "player", StringComparison.OrdinalIgnoreCase);

            if (isPublic && isPlayer)
            {
                var keySearch = 0;
                while (true)
                {
                    var keyToken = segment.IndexOf("\"key\"", keySearch, StringComparison.Ordinal);
                    if (keyToken < 0)
                        break;

                    var keyValue = CloudSaveResponseParser.ReadJsonStringField(segment, "key", keyToken);
                    if (!string.IsNullOrEmpty(keyValue))
                        keys.Add(keyValue);

                    keySearch = keyToken + 5;
                }
            }

            searchFrom = accessClassIndex + 1;
        }
    }

    async Task CreatePublicPlayerIndexAsync(
        string authorizationHeader,
        string environmentId,
        string fieldKey,
        CancellationToken cancellationToken)
    {
        var url = new StringBuilder()
            .Append(CloudSaveAdminBaseUrl)
            .Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(environmentId.Trim()))
            .Append("/indexes/players/public")
            .ToString();

        var body = new StringBuilder()
            .Append("{\"indexConfig\":{\"fields\":[{\"key\":\"")
            .Append(EscapeJson(fieldKey))
            .Append("\",\"asc\":true}]}}")
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbPOST,
            body,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" },
                { "Content-Type", "application/json" }
            },
            cancellationToken);
    }

    async Task SetDefaultCustomItemAsync(
        string authorizationHeader,
        string environmentId,
        string customId,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        var url = new StringBuilder()
            .Append(CloudSaveAdminBaseUrl)
            .Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(environmentId.Trim()))
            .Append("/custom/").Append(Uri.EscapeDataString(customId))
            .Append("/items")
            .ToString();

        var body = new StringBuilder()
            .Append("{\"key\":\"")
            .Append(EscapeJson(key))
            .Append("\",\"value\":\"")
            .Append(EscapeJson(value))
            .Append("\"}")
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbPOST,
            body,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" },
                { "Content-Type", "application/json" }
            },
            cancellationToken);
    }

    async Task<HashSet<string>> ListExistingTriggerNamesAsync(
        string authorizationHeader,
        string environmentId,
        CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var url = new StringBuilder()
            .Append(TriggersAdminBaseUrl)
            .Append("/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(environmentId.Trim()))
            .Append("/configs?limit=100")
            .ToString();

        string responseText;
        try
        {
            responseText = await SendAsync(
                url,
                UnityWebRequest.kHttpVerbGET,
                null,
                new Dictionary<string, string>
                {
                    { "Authorization", authorizationHeader },
                    { "Accept", "application/json" }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CloudSaveAdminQuery] List triggers failed (will still try create):\n{ex.Message}");
            return names;
        }

        // Only treat known trigger names as existing to avoid false skips from unrelated "name" fields.
        var known = new HashSet<string>(
            CloudSaveTriggers.Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        var searchFrom = 0;
        while (true)
        {
            var nameIndex = responseText.IndexOf("\"name\"", searchFrom, StringComparison.Ordinal);
            if (nameIndex < 0)
                break;

            var name = CloudSaveResponseParser.ReadJsonStringField(responseText, "name", nameIndex);
            if (!string.IsNullOrEmpty(name) && known.Contains(name))
                names.Add(name);

            searchFrom = nameIndex + 1;
        }

        return names;
    }

    async Task CreateCloudSaveTriggerAsync(
        string authorizationHeader,
        string environmentId,
        CloudSaveTriggerSpec trigger,
        CancellationToken cancellationToken)
    {
        var url = new StringBuilder()
            .Append(TriggersAdminBaseUrl)
            .Append("/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(environmentId.Trim()))
            .Append("/configs")
            .ToString();

        var actionUrn = $"urn:ugs:cloud-code:{CloudCodeModuleName}/{trigger.FunctionName}";
        // CEL uses single-quoted strings; Unity docs use .matches('^prefix.*') for starts-with.
        var filter =
            $"data['idType'] == 'player' && data['key'].matches('^{trigger.KeyPrefix}.*')";

        var body = new StringBuilder()
            .Append("{\"name\":\"").Append(EscapeJson(trigger.Name))
            .Append("\",\"eventType\":\"").Append(EscapeJson(CloudSaveKeySavedEventType))
            .Append("\",\"actionType\":\"cloud-code\"")
            .Append(",\"actionUrn\":\"").Append(EscapeJson(actionUrn))
            .Append("\",\"filter\":\"").Append(EscapeJson(filter))
            .Append("\"}")
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbPOST,
            body,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" },
                { "Content-Type", "application/json" }
            },
            cancellationToken);
    }

    readonly struct CloudSaveTriggerSpec
    {
        public readonly string Name;
        public readonly string KeyPrefix;
        public readonly string FunctionName;

        public CloudSaveTriggerSpec(string name, string keyPrefix, string functionName)
        {
            Name = name;
            KeyPrefix = keyPrefix;
            FunctionName = functionName;
        }
    }

    static bool IsHttpConflict(Exception ex)
    {
        if (ex == null || string.IsNullOrEmpty(ex.Message))
            return false;

        return ex.Message.IndexOf("HTTP 409", StringComparison.Ordinal) >= 0
               || ex.Message.IndexOf("\"status\":409", StringComparison.Ordinal) >= 0;
    }

    static bool IsHttpNotFound(Exception ex)
    {
        if (ex == null || string.IsNullOrEmpty(ex.Message))
            return false;

        return ex.Message.IndexOf("HTTP 404", StringComparison.Ordinal) >= 0
               || ex.Message.IndexOf("\"status\":404", StringComparison.Ordinal) >= 0;
    }

    string BuildTriggersAuthorizationHeader(out string error)
    {
        // Triggers Admin API requires Service Account Basic auth.
        if (!string.IsNullOrWhiteSpace(_serviceKeyId) && !string.IsNullOrWhiteSpace(_serviceSecretKey))
        {
            error = null;
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_serviceKeyId.Trim()}:{_serviceSecretKey}"));
            return $"Basic {credentials}";
        }

        error =
            "Triggers require Service Account Key ID + Secret (Basic auth). " +
            "Bearer-only auth is not supported by the Triggers Admin API.";
        return null;
    }

    static string FormatApiError(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        var firstLine = message;
        var newline = message.IndexOf('\n');
        if (newline >= 0)
            firstLine = message.Substring(0, newline).Trim();

        var responseIdx = message.IndexOf("Response:", StringComparison.Ordinal);
        if (responseIdx >= 0)
        {
            var response = message.Substring(responseIdx + "Response:".Length).Trim();
            var detail = CloudSaveResponseParser.ReadJsonStringField(response, "detail", 0);
            if (string.IsNullOrEmpty(detail))
                detail = CloudSaveResponseParser.ReadJsonStringField(response, "title", 0);

            if (string.IsNullOrEmpty(detail))
            {
                var messagesIdx = response.IndexOf("\"messages\"", StringComparison.Ordinal);
                if (messagesIdx >= 0)
                {
                    var arrayStart = response.IndexOf('[', messagesIdx);
                    var arrayEnd = arrayStart >= 0 ? response.IndexOf(']', arrayStart) : -1;
                    if (arrayStart >= 0 && arrayEnd > arrayStart)
                        detail = response.Substring(arrayStart + 1, arrayEnd - arrayStart - 1)
                            .Replace("\"", string.Empty)
                            .Trim();
                }
            }

            if (!string.IsNullOrEmpty(detail))
            {
                var combined = firstLine + " — " + detail.Replace('\n', ' ').Trim();
                return combined.Length <= 320 ? combined : combined.Substring(0, 320) + "...";
            }
        }

        return firstLine.Length <= 200 ? firstLine : firstLine.Substring(0, 200) + "...";
    }

    static string TruncateStatus(string message) => FormatApiError(message);

    async Task RunQueryAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var cancellationToken = _fetchCts.Token;

        _isBusy = true;
        SetStatusMessage(_queryMode == QueryMode.ListAllPlayers ? "Listing players..." : "Searching...");
        Repaint();

        try
        {
            if (!ValidateAuthInputs())
                return;

            var authorizationHeader = BuildAuthorizationHeader();

            if (_queryMode == QueryMode.ListAllPlayers)
            {
                var count = await ListAllPlayersStreamingAsync(authorizationHeader, cancellationToken);
                SetStatusMessage($"Listed {count} player(s).");
            }
            else
            {
                var rows = await SearchPlayersAsync(authorizationHeader, cancellationToken);
                if (rows != null)
                    SetResults(rows);
                SetStatusMessage($"Found {_resultRows.Count} player(s).");
            }
        }
        catch (OperationCanceledException)
        {
            if (_queryMode == QueryMode.ListAllPlayers)
                SetStatusMessage($"Fetch stopped. {_resultRows.Count} player(s) loaded.");
            else
                SetStatusMessage("Search stopped.");
        }
        catch (Exception ex)
        {
            SetStatusMessage("Request failed.");
            if (_queryMode != QueryMode.ListAllPlayers || _resultRows.Count == 0)
                ClearResults();
            Debug.LogException(ex);
        }
        finally
        {
            _fetchCts?.Dispose();
            _fetchCts = null;
            _isBusy = false;
            Repaint();
        }
    }

    async Task FetchPlayerManagementAmountAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var cancellationToken = _fetchCts.Token;

        _isBusy = true;
        SetStatusMessage("Fetching Player Management amount...");
        Repaint();

        try
        {
            if (!ValidateAuthInputs())
                return;

            var authorizationHeader = BuildAuthorizationHeader();
            var amount = await CountPlayerManagementPlayersAsync(authorizationHeader, cancellationToken);
            _playerManagementAmount = amount;
            SetStatusMessage($"Player Management players: {amount}");
        }
        catch (OperationCanceledException)
        {
            SetStatusMessage(_playerManagementAmount.HasValue
                ? $"Fetch stopped. Player Management players so far: {_playerManagementAmount.Value}"
                : "Player Management amount fetch stopped.");
        }
        catch (Exception ex)
        {
            SetStatusMessage("Player Management amount request failed.");
            Debug.LogException(ex);
        }
        finally
        {
            _fetchCts?.Dispose();
            _fetchCts = null;
            _isBusy = false;
            Repaint();
        }
    }

    async Task<int> CountPlayerManagementPlayersAsync(string authorizationHeader, CancellationToken cancellationToken)
    {
        var total = 0;
        string page = null;
        var iterations = 0;

        while (iterations++ < PaginationSafetyLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetStatusMessage($"Fetching Player Management amount... {total}");
            Repaint();

            var url = new StringBuilder()
                .Append(PlayerAuthAdminBaseUrl)
                .Append("/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
                .Append("/users?limit=").Append(CloudSaveResponseParser.PlayerManagementListPageSize)
                .ToString();

            if (!string.IsNullOrEmpty(page))
                url += "&page=" + Uri.EscapeDataString(page);

            var responseText = await SendAsync(
                url,
                UnityWebRequest.kHttpVerbGET,
                null,
                new Dictionary<string, string>
                {
                    { "Authorization", authorizationHeader },
                    { "Accept", "application/json" }
                },
                cancellationToken);

            var pageCount = CloudSaveResponseParser.CountResultsArray(responseText);
            total += pageCount;
            _playerManagementAmount = total;

            var nextPage = CloudSaveResponseParser.ReadNextPageToken(responseText);
            if (string.IsNullOrEmpty(nextPage) || pageCount == 0)
                break;

            page = nextPage;
        }

        return total;
    }

    async Task FetchCloudSaveEntityAmountAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var cancellationToken = _fetchCts.Token;

        _isBusy = true;
        SetStatusMessage("Fetching Cloud Save entity amount...");
        Repaint();

        try
        {
            if (!ValidateAuthInputs())
                return;

            var authorizationHeader = BuildAuthorizationHeader();
            var amount = await CountCloudSaveEntitiesAsync(authorizationHeader, cancellationToken);
            _cloudSaveEntityAmount = amount;
            SetStatusMessage($"Cloud Save entities: {amount:N0}");
        }
        catch (OperationCanceledException)
        {
            SetStatusMessage(_cloudSaveEntityAmount.HasValue
                ? $"Fetch stopped. Cloud Save entities so far: {_cloudSaveEntityAmount.Value:N0}"
                : "Cloud Save entity amount fetch stopped.");
        }
        catch (Exception ex)
        {
            SetStatusMessage("Cloud Save entity amount request failed. " + FormatApiError(ex.Message));
            Debug.LogException(ex);
        }
        finally
        {
            _fetchCts?.Dispose();
            _fetchCts = null;
            _isBusy = false;
            Repaint();
        }
    }

    async Task<int> CountCloudSaveEntitiesAsync(string authorizationHeader, CancellationToken cancellationToken)
    {
        var total = 0;
        var seenPlayerIds = new HashSet<string>();
        string start = null;
        var iterations = 0;
        var pageSize = CloudSaveResponseParser.CloudSaveEntitiesListPageSize;

        while (iterations++ < PaginationSafetyLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetStatusMessage($"Fetching Cloud Save entity amount... {total:N0}");
            Repaint();

            var page = await FetchPlayersListPageAsync(authorizationHeader, start, pageSize, cancellationToken);
            if (page.PlayerIds.Count == 0 && string.IsNullOrEmpty(page.NextStart))
                break;

            foreach (var playerId in page.PlayerIds)
            {
                if (seenPlayerIds.Add(playerId))
                    total++;
            }

            _cloudSaveEntityAmount = total;

            if (string.IsNullOrEmpty(page.NextStart) || page.NextStart == start)
                break;

            start = page.NextStart;
        }

        return total;
    }

    async Task DeletePlayerAsync(string playerId = null)
    {
        playerId = playerId?.Trim() ?? _deletePlayerId?.Trim();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            SetStatusMessage("Player ID is required for delete.");
            return;
        }

        _deletePlayerId = playerId;

        if (!EditorUtility.DisplayDialog(
                "Delete player completely",
                $"Permanently delete player and all related data:\n{playerId}\n\n" +
                "• Cloud Save (public / protected / default)\n" +
                "• Leaderboards scores\n" +
                "• Authentication account\n\nThis cannot be undone.",
                "Delete",
                "Cancel"))
        {
            return;
        }

        _isBusy = true;
        SetStatusMessage("Deleting player completely...");
        Repaint();

        try
        {
            if (!ValidateAuthInputs())
                return;

            var authorizationHeader = BuildAuthorizationHeader();
            if (string.IsNullOrWhiteSpace(_environmentId))
                _environmentId = ReadEnvironmentId();
            if (string.IsNullOrWhiteSpace(_environmentId))
            {
                SetStatusMessage("Environment ID is required for delete.");
                return;
            }

            var errors = new List<string>();

            async Task TryStep(string label, Func<Task> action)
            {
                SetStatusMessage($"{label}...");
                Repaint();
                try
                {
                    await action();
                }
                catch (Exception ex) when (IsHttpNotFound(ex))
                {
                    // Already gone — fine.
                }
                catch (Exception ex)
                {
                    errors.Add($"{label}: {TruncateStatus(ex.Message)}");
                    Debug.LogWarning($"[CloudSaveAdminQuery] {label} failed:\n{ex.Message}");
                }
            }

            await TryStep("Deleting Cloud Save public data",
                () => DeleteCloudSaveAccessClassAsync(authorizationHeader, playerId, "public"));
            await TryStep("Deleting Cloud Save protected data",
                () => DeleteCloudSaveAccessClassAsync(authorizationHeader, playerId, "protected"));
            await TryStep("Deleting Cloud Save default data",
                () => DeleteCloudSaveAccessClassAsync(authorizationHeader, playerId, "default"));
            await TryStep("Purging Leaderboards scores",
                () => PurgeLeaderboardPlayerScoresAsync(authorizationHeader, playerId));
            await TryStep("Deleting Authentication player",
                () => DeleteAuthenticationPlayerAsync(authorizationHeader, playerId));

            _resultRows.RemoveAll(row => GetRowValue(row, "playerId") == playerId);
            _usersTreeView?.ReloadRows();

            if (errors.Count == 0)
                SetStatusMessage($"Deleted player completely: {playerId}");
            else
                SetStatusMessage($"Player delete finished with issues for {playerId}: " + string.Join(" | ", errors));
        }
        catch (Exception ex)
        {
            SetStatusMessage("Delete failed.");
            Debug.LogException(ex);
        }
        finally
        {
            _isBusy = false;
            Repaint();
        }
    }

    async Task<List<Dictionary<string, string>>> SearchPlayersAsync(
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        var apiFilters = BuildApiFilters(out var filterError);
        var missingKeys = CollectMissingFieldKeys();
        if (filterError != null)
        {
            SetStatusMessage(filterError);
            return new List<Dictionary<string, string>>();
        }

        if (apiFilters.Count == 0 && missingKeys.Count == 0)
        {
            SetStatusMessage("Enter at least one search field.");
            return new List<Dictionary<string, string>>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Indexed queries only match items that have the field. Missing-field search
        // lists players and keeps those whose public data does not contain the key.
        if (apiFilters.Count == 0)
        {
            await ListAllPlayersStreamingAsync(
                authorizationHeader,
                cancellationToken,
                row => RowIsMissingAllFields(row, missingKeys));
            return null;
        }

        var rows = await QueryPlayersAsync(authorizationHeader, apiFilters, cancellationToken);
        if (missingKeys.Count > 0)
            rows.RemoveAll(row => !RowIsMissingAllFields(row, missingKeys));
        return rows;
    }

    async Task<List<Dictionary<string, string>>> QueryPlayersAsync(
        string authorizationHeader,
        List<QueryFieldFilter> apiFilters,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, string>>();
        var limit = Mathf.Clamp(_resultLimit, 1, 100);
        var offset = 0;
        var pagesFetched = 0;

        while (pagesFetched < PaginationSafetyLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pagesFetched++;

            SetStatusMessage($"Searching... batch {pagesFetched}");
            Repaint();

            var queryResponse = await QueryPublicPlayerDataAsync(
                authorizationHeader,
                apiFilters,
                limit,
                offset,
                cancellationToken);
            var pageRows = ParseQueryRows(queryResponse);
            if (pageRows.Count == 0)
                break;

            foreach (var row in pageRows)
            {
                CloudSaveTimeFormat.NormalizeRow(row);
                rows.Add(row);
            }

            if (pageRows.Count < limit)
                break;

            offset += limit;
        }

        return rows;
    }

    List<QueryFieldFilter> BuildApiFilters(out string error)
    {
        error = null;
        var filters = new List<QueryFieldFilter>();

        AddStringFilter(filters, "ip", _ip, _ipMatchMissing);
        AddStringFilter(filters, "adId", _adId, _adIdMatchMissing);
        AddStringFilter(filters, "deviceID", _deviceId, _deviceIdMatchMissing);
        AddStringFilter(filters, "androidId", _androidId, _androidIdMatchMissing);
        AddStringFilter(filters, "pushToken", _pushToken, _pushTokenMatchMissing);
        AddStringFilter(filters, "devModel", _devModel, _devModelMatchMissing);

        if (!_dateUtcServerMatchMissing && !string.IsNullOrWhiteSpace(_dateUtcServer))
        {
            if (!CloudSaveTimeFormat.TryFormatDateForCloudSaveQuery(_dateUtcServer.Trim(), out var queryValue))
            {
                error = "Invalid date_utc_server. Use dd/MM/yyyy.";
                return filters;
            }

            filters.Add(new QueryFieldFilter("date_utc_server", queryValue));
        }

        AddStringFilter(filters, "playerId", _playerIdFilter, _playerIdMatchMissing);

        if (!_isPrivacyMatchMissing && !string.IsNullOrWhiteSpace(_isPrivacy))
        {
            var privacy = _isPrivacy.Trim().ToLowerInvariant();
            if (privacy != "true" && privacy != "false")
            {
                error = "isPrivacy must be true or false.";
                return filters;
            }

            filters.Add(new QueryFieldFilter("isPrivacy", privacy, "EQ"));
        }

        AddStringFilter(filters, "campaign", _campaign, _campaignMatchMissing);
        AddStringFilter(filters, "network", _network, _networkMatchMissing);

        if (!_hourMatchMissing && !string.IsNullOrWhiteSpace(_hour))
        {
            var hourText = _hour.Trim();
            if (!int.TryParse(hourText, out var hour) || hour < 0 || hour > 23)
            {
                error = "hour must be an integer from 0 to 23.";
                return filters;
            }

            filters.Add(new QueryFieldFilter("hour", hour.ToString()));
        }

        if (!_monthMatchMissing && !string.IsNullOrWhiteSpace(_month))
        {
            var monthText = _month.Trim();
            if (!int.TryParse(monthText, out var month) || month < 1 || month > 12)
            {
                error = "month must be an integer from 1 to 12.";
                return filters;
            }

            filters.Add(new QueryFieldFilter("month", month.ToString()));
        }

        if (!_dayMatchMissing && !string.IsNullOrWhiteSpace(_day))
        {
            var dayText = _day.Trim();
            if (!int.TryParse(dayText, out var day) || day < 1 || day > 31)
            {
                error = "day must be an integer from 1 to 31.";
                return filters;
            }

            filters.Add(new QueryFieldFilter("day", day.ToString()));
        }

        return filters;
    }

    List<string> CollectMissingFieldKeys()
    {
        var keys = new List<string>();
        if (_ipMatchMissing) keys.Add("ip");
        if (_adIdMatchMissing) keys.Add("adId");
        if (_deviceIdMatchMissing) keys.Add("deviceID");
        if (_androidIdMatchMissing) keys.Add("androidId");
        if (_pushTokenMatchMissing) keys.Add("pushToken");
        if (_devModelMatchMissing) keys.Add("devModel");
        if (_dateUtcServerMatchMissing) keys.Add("date_utc_server");
        if (_playerIdMatchMissing) keys.Add("playerId");
        if (_isPrivacyMatchMissing) keys.Add("isPrivacy");
        if (_campaignMatchMissing) keys.Add("campaign");
        if (_networkMatchMissing) keys.Add("network");
        if (_hourMatchMissing) keys.Add("hour");
        if (_monthMatchMissing) keys.Add("month");
        if (_dayMatchMissing) keys.Add("day");
        return keys;
    }

    static bool RowIsMissingAllFields(Dictionary<string, string> row, List<string> keys)
    {
        if (keys == null || keys.Count == 0)
            return true;

        for (var i = 0; i < keys.Count; i++)
        {
            if (!RowIsMissingField(row, keys[i]))
                return false;
        }

        return true;
    }

    static bool RowIsMissingField(Dictionary<string, string> row, string key)
    {
        if (row == null || string.IsNullOrEmpty(key))
            return true;

        return !row.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value);
    }

    static void AddStringFilter(List<QueryFieldFilter> filters, string key, string value, bool matchMissing)
    {
        if (matchMissing || string.IsNullOrWhiteSpace(value))
            return;

        filters.Add(new QueryFieldFilter(key, value.Trim()));
    }

    bool ValidateAuthInputs()
    {
        if (string.IsNullOrWhiteSpace(_projectId))
        {
            SetStatusMessage("Project ID is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_environmentId))
        {
            SetStatusMessage("Environment ID is required.");
            return false;
        }

        if (_useBearerToken)
        {
            if (string.IsNullOrWhiteSpace(_bearerToken))
            {
                SetStatusMessage("Bearer token is required.");
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(_serviceKeyId) || string.IsNullOrWhiteSpace(_serviceSecretKey))
        {
            SetStatusMessage("Service account key ID and secret are required.");
            return false;
        }

        return true;
    }

    string BuildAuthorizationHeader()
    {
        if (_useBearerToken)
            return $"Bearer {_bearerToken.Trim()}";

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_serviceKeyId.Trim()}:{_serviceSecretKey}"));
        return $"Basic {credentials}";
    }

    bool ShouldStopListingPlayers(int loadedPlayerCount) =>
        _listPlayerLimit > 0 && loadedPlayerCount >= _listPlayerLimit;

    async Task<int> ListAllPlayersStreamingAsync(
        string authorizationHeader,
        CancellationToken cancellationToken,
        Func<Dictionary<string, string>, bool> includeRow = null)
    {
        InitializeStreamingResults();

        var scanned = 0;
        var matched = 0;
        var seenPlayerIds = new HashSet<string>();
        string start = null;
        var iterations = 0;
        var filteringMissing = includeRow != null;

        while (iterations++ < PaginationSafetyLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetStatusMessage(filteringMissing
                ? $"Searching missing fields... batch {iterations}"
                : $"Listing players... batch {iterations}");
            Repaint();

            var page = await FetchPlayersListPageAsync(authorizationHeader, start, cancellationToken);
            if (page.PlayerIds.Count == 0 && string.IsNullOrEmpty(page.NextStart))
                break;

            var newPlayersOnPage = 0;
            foreach (var playerId in page.PlayerIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!seenPlayerIds.Add(playerId))
                    continue;

                newPlayersOnPage++;

                if (ShouldStopListingPlayers(scanned))
                    return matched;

                SetStatusMessage(filteringMissing
                    ? $"Searching missing fields... scanned {scanned + 1}, matched {matched} ({playerId})"
                    : $"Loading public data... {scanned + 1} ({playerId})");
                Repaint();

                Dictionary<string, string> row;
                try
                {
                    row = await LoadPlayerRowAsync(authorizationHeader, playerId, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    row = new Dictionary<string, string>
                    {
                        ["playerId"] = playerId,
                        ["error"] = ex.Message
                    };
                }

                scanned++;
                if (includeRow != null && !includeRow(row))
                    continue;

                AppendResultRow(row);
                matched++;
            }

            if (ShouldStopListingPlayers(scanned))
                return matched;

            if (newPlayersOnPage == 0)
                break;

            if (string.IsNullOrEmpty(page.NextStart) || page.NextStart == start)
                break;

            start = page.NextStart;
        }

        return matched;
    }

    async Task<PlayersListPage> FetchPlayersListPageAsync(
        string authorizationHeader,
        string start,
        CancellationToken cancellationToken)
    {
        return await FetchPlayersListPageAsync(
            authorizationHeader,
            start,
            CloudSaveResponseParser.PlayersListApiPageSize,
            cancellationToken);
    }

    async Task<PlayersListPage> FetchPlayersListPageAsync(
        string authorizationHeader,
        string start,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var url = BuildListPlayersUrl(start, pageSize);
        var responseText = await SendAsync(
            url,
            UnityWebRequest.kHttpVerbGET,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            },
            cancellationToken);

        CloudSaveResponseParser.ParsePlayersListPage(
            responseText,
            start,
            pageSize,
            out var playerIds,
            out var nextStart);
        return new PlayersListPage(playerIds, nextStart);
    }

    readonly struct PlayersListPage
    {
        public readonly List<string> PlayerIds;
        public readonly string NextStart;

        public PlayersListPage(List<string> playerIds, string nextStart)
        {
            PlayerIds = playerIds ?? new List<string>();
            NextStart = nextStart;
        }
    }

    async Task<Dictionary<string, string>> LoadPlayerRowAsync(
        string authorizationHeader,
        string playerId,
        CancellationToken cancellationToken)
    {
        var row = new Dictionary<string, string> { ["playerId"] = playerId };
        string after = null;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemsResponse = await GetPublicItemsAsync(authorizationHeader, playerId, cancellationToken, after);
            var page = CloudSaveResponseParser.ParsePublicItemsPage(itemsResponse, out after);
            foreach (var item in page)
                row[item.Key] = item.Value;
        }
        while (!string.IsNullOrEmpty(after));

        CloudSaveTimeFormat.NormalizeRow(row);
        return row;
    }

    string BuildListPlayersUrl(string start, int limit)
    {
        var sb = new StringBuilder();
        sb.Append(CloudSaveAdminBaseUrl);
        sb.Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()));
        sb.Append("/environments/").Append(Uri.EscapeDataString(_environmentId.Trim()));
        sb.Append("/players?limit=").Append(limit);

        if (!string.IsNullOrWhiteSpace(start))
            sb.Append("&start=").Append(Uri.EscapeDataString(start));

        return sb.ToString();
    }

    async Task<string> GetPublicItemsAsync(
        string authorizationHeader,
        string playerId,
        CancellationToken cancellationToken,
        string after = null)
    {
        var sb = new StringBuilder();
        sb.Append(CloudSaveAdminBaseUrl);
        sb.Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()));
        sb.Append("/environments/").Append(Uri.EscapeDataString(_environmentId.Trim()));
        sb.Append("/players/").Append(Uri.EscapeDataString(playerId));
        sb.Append("/public/items");

        for (var i = 0; i < ReturnKeys.Length; i++)
            sb.Append(i == 0 ? '?' : '&').Append("keys=").Append(Uri.EscapeDataString(ReturnKeys[i]));

        if (!string.IsNullOrWhiteSpace(after))
            sb.Append("&after=").Append(Uri.EscapeDataString(after));

        return await SendAsync(
            sb.ToString(),
            UnityWebRequest.kHttpVerbGET,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            },
            cancellationToken);
    }

    async Task DeleteCloudSaveAccessClassAsync(string authorizationHeader, string playerId, string accessClass)
    {
        // public → /public/items, protected → /protected/items, default → /items
        var suffix = accessClass switch
        {
            "public" => "/public/items",
            "protected" => "/protected/items",
            _ => "/items"
        };

        var url = new StringBuilder()
            .Append(CloudSaveAdminBaseUrl)
            .Append("/data/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(_environmentId.Trim()))
            .Append("/players/").Append(Uri.EscapeDataString(playerId))
            .Append(suffix)
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbDELETE,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            });
    }

    async Task PurgeLeaderboardPlayerScoresAsync(string authorizationHeader, string playerId)
    {
        if (string.IsNullOrWhiteSpace(_environmentId))
            _environmentId = ReadEnvironmentId();

        var url = new StringBuilder()
            .Append(LeaderboardsAdminBaseUrl)
            .Append("/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/environments/").Append(Uri.EscapeDataString(_environmentId.Trim()))
            .Append("/leaderboards/scores/players/")
            .Append(Uri.EscapeDataString(playerId))
            .Append("/purge")
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbDELETE,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            });
    }

    async Task DeleteAuthenticationPlayerAsync(string authorizationHeader, string playerId)
    {
        var url = new StringBuilder()
            .Append(PlayerAuthAdminBaseUrl)
            .Append("/projects/").Append(Uri.EscapeDataString(_projectId.Trim()))
            .Append("/users/").Append(Uri.EscapeDataString(playerId))
            .ToString();

        await SendAsync(
            url,
            UnityWebRequest.kHttpVerbDELETE,
            null,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" }
            });
    }

    async Task DeletePublicItemsAsync(string authorizationHeader, string playerId) =>
        await DeleteCloudSaveAccessClassAsync(authorizationHeader, playerId, "public");

    async Task<string> QueryPublicPlayerDataAsync(
        string authorizationHeader,
        List<QueryFieldFilter> filters,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var url = $"{CloudSaveAdminBaseUrl}/data/projects/{Uri.EscapeDataString(_projectId.Trim())}/environments/{Uri.EscapeDataString(_environmentId.Trim())}/players/public/query";
        var body = BuildQueryBody(filters, limit, offset);

        return await SendAsync(
            url,
            UnityWebRequest.kHttpVerbPOST,
            body,
            new Dictionary<string, string>
            {
                { "Authorization", authorizationHeader },
                { "Accept", "application/json" },
                { "Content-Type", "application/json" }
            },
            cancellationToken);
    }

    static string BuildQueryBody(List<QueryFieldFilter> filters, int limit, int offset = 0)
    {
        var sb = new StringBuilder();
        sb.Append("{\"fields\":[");
        for (var i = 0; i < filters.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append("{\"key\":\"").Append(EscapeJson(filters[i].key));
            sb.Append("\",\"op\":\"").Append(EscapeJson(filters[i].op));
            sb.Append("\",\"value\":");
            if (filters[i].valueAsJsonLiteral)
                sb.Append(filters[i].value);
            else
                sb.Append('"').Append(EscapeJson(filters[i].value)).Append('"');
            sb.Append(",\"asc\":true}");
        }

        sb.Append("],\"returnKeys\":[");
        for (var i = 0; i < ReturnKeys.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('"').Append(EscapeJson(ReturnKeys[i])).Append('"');
        }

        sb.Append("],\"limit\":").Append(Mathf.Clamp(limit, 1, 100));
        if (offset > 0)
            sb.Append(",\"offset\":").Append(offset);
        sb.Append('}');
        return sb.ToString();
    }

    static List<Dictionary<string, string>> ParseQueryRows(string rawJson) =>
        CloudSaveResponseParser.ParseQueryResults(rawJson);

    static string GetRowValue(Dictionary<string, string> row, string key) =>
        row != null && row.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

    readonly struct HttpTextResponse
    {
        public readonly long StatusCode;
        public readonly string Body;
        public readonly string Error;

        public HttpTextResponse(long statusCode, string body, string error)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
            Error = error ?? string.Empty;
        }
    }

    async Task<HttpTextResponse> SendWithStatusAsync(
        string url,
        string method,
        string jsonBody,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        using var request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 120;

        if (!string.IsNullOrEmpty(jsonBody))
        {
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key == "Content-Type" && request.uploadHandler != null)
                    continue;
                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        await SendUnityWebRequestAsync(request, cancellationToken);
        return new HttpTextResponse(
            request.responseCode,
            request.downloadHandler?.text,
            request.error);
    }

    async Task SendMultipartAsync(
        string url,
        string method,
        List<IMultipartFormSection> form,
        string authorizationHeader,
        CancellationToken cancellationToken)
    {
        using var request = UnityWebRequest.Post(url, form);
        request.method = method;
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 300;
        request.SetRequestHeader("Authorization", authorizationHeader);
        await SendUnityWebRequestAsync(request, cancellationToken);

#if UNITY_2020_1_OR_NEWER
        var failed = request.result != UnityWebRequest.Result.Success;
#else
        var failed = request.isNetworkError || request.isHttpError;
#endif
        if (failed)
            throw new InvalidOperationException(BuildRequestError(request, "(multipart module upload)"));
    }

    static async Task SendUnityWebRequestAsync(UnityWebRequest request, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.CanBeCanceled
            ? (IDisposable)cancellationToken.Register(() => request.Abort())
            : null;

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                request.Abort();
                cancellationToken.ThrowIfCancellationRequested();
            }

            await Task.Yield();
        }

        if (cancellationToken.IsCancellationRequested)
            cancellationToken.ThrowIfCancellationRequested();
    }

    async Task<string> SendAsync(
        string url,
        string method,
        string jsonBody,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        using var request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();

        if (!string.IsNullOrEmpty(jsonBody))
        {
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key == "Content-Type" && request.uploadHandler != null)
                    continue;
                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        using var registration = cancellationToken.CanBeCanceled
            ? (IDisposable)cancellationToken.Register(() => request.Abort())
            : null;

        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                request.Abort();
                cancellationToken.ThrowIfCancellationRequested();
            }

            await Task.Yield();
        }

        if (cancellationToken.IsCancellationRequested)
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_2020_1_OR_NEWER
        if (request.result != UnityWebRequest.Result.Success)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
            throw new InvalidOperationException(BuildRequestError(request, jsonBody));

        return request.downloadHandler.text;
    }

    static string BuildRequestError(UnityWebRequest request, string requestBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HTTP {(long)request.responseCode} {request.error}");
        sb.AppendLine($"URL: {request.url}");
        if (!string.IsNullOrEmpty(requestBody))
        {
            sb.AppendLine("Request body:");
            sb.AppendLine(requestBody);
        }

        if (!string.IsNullOrEmpty(request.downloadHandler?.text))
        {
            sb.AppendLine("Response:");
            sb.AppendLine(request.downloadHandler.text);
        }

        return sb.ToString();
    }

    static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    static string ReadCloudProjectId()
    {
        try
        {
            if (CloudProjectSettings.projectBound && !string.IsNullOrWhiteSpace(CloudProjectSettings.projectId))
                return CloudProjectSettings.projectId.Trim();
        }
        catch
        {
            // Fall back to project settings file below.
        }

        return ReadYamlValue(
            Path.Combine(GetProjectRoot(), "ProjectSettings", "ProjectSettings.asset"),
            "cloudProjectId:").Trim();
    }

    static string ReadEnvironmentId()
    {
        var settingsPath = Path.Combine(
            GetProjectRoot(),
            "ProjectSettings",
            "Packages",
            "com.unity.services.core",
            "Settings.json");

        if (!File.Exists(settingsPath))
            return string.Empty;

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonUtility.FromJson<ServicesCoreSettings>(json);
            if (!string.IsNullOrWhiteSpace(settings?.EnvironmentId))
                return settings.EnvironmentId.Trim();
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    static string ReadYamlValue(string path, string key)
    {
        if (!File.Exists(path))
            return string.Empty;

        try
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith(key, StringComparison.Ordinal))
                    continue;

                return trimmed.Substring(key.Length).Trim();
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    static string GetProjectRoot()
    {
        return Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
    }

    readonly struct QueryFieldFilter
    {
        public readonly string key;
        public readonly string op;
        public readonly string value;
        public readonly bool valueAsJsonLiteral;

        public QueryFieldFilter(string key, string value, string op = "EQ", bool valueAsJsonLiteral = false)
        {
            this.key = key;
            this.value = value;
            this.op = op;
            this.valueAsJsonLiteral = valueAsJsonLiteral;
        }
    }

    [Serializable]
    sealed class ServicesCoreSettings
    {
        public string EnvironmentName;
        public string EnvironmentId;
    }
}
#endif
