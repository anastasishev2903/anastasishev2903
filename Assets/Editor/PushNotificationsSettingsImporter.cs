#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class PushNotificationsSettingsImporterWindow : EditorWindow
{
    const string SettingsAssetPath = "Assets/Resources/pushNotificationsSettings.asset";
    const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
    const string GameIconAssetPath = "Assets/Art/Textures/game_icon.png";
    const int GameIconSize = 512;
    const string LastJsonDirectoryPref = "PushNotificationsSettingsImporter_LastJsonDirectory";
    const string LastKeystoreDirectoryPref = "PushNotificationsSettingsImporter_LastKeystoreDirectory";

    string _jsonText = string.Empty;
    string _jsonFilePath = string.Empty;
    string _appName = string.Empty;
    string _version = string.Empty;
    string _buildNumber = string.Empty;
    string _adjustToken = string.Empty;
    string _adjustS2sToken = string.Empty;
    string _keystorePassword = string.Empty;
    string _customKeystorePath = string.Empty;
    string _statusMessage = string.Empty;
    Vector2 _jsonScroll;

    [MenuItem("Tools/Import Firebase JSON to Push Settings")]
    static void OpenWindow()
    {
        var window = GetWindow<PushNotificationsSettingsImporterWindow>("Firebase Push Import");
        window.minSize = new Vector2(520f, 600f);
        window.Show();
    }

    void OnEnable()
    {
        _keystorePassword = PushNotificationsProjectSigningStore.Load().KeystorePassword;
    }

    void OnDisable()
    {
        PushNotificationsProjectSigningStore.Save(_keystorePassword);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Project", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "App Name updates Player Settings product name when Apply is clicked. " +
            "Left empty, the current product name is kept.\n" +
            "Version updates Player Settings bundle version when filled and different. " +
            "Not stored; left empty or unchanged keeps the current version.\n" +
            "Build Number updates Player Settings Android bundle version code when filled and different. " +
            "Not stored; left empty or unchanged keeps the current build number.\n" +
            "Keystore Password creates release.keystore at the project root when filled. " +
            "Left empty or unchanged, the current keystore settings are kept. " +
            "Skipped if a keystore already exists for this project.\n" +
            "Custom Keystore copies a selected .keystore to the project root using its original file name, " +
            "points Player Settings at that project-root file, uses the first key alias from the keystore, " +
            "applies the password, and saves it the same way as keystore creation.\n" +
            "Keystore password is saved to EditorPrefs and editor-android-signing.json at the project root " +
            "(plain text, safe to commit to git, not included in builds) and restored into Player Settings on project open.\n" +
            "JSON left empty skips push notifications data, package, and manifest updates.\n" +
            "Adjust Token replaces APP_TOKEN in Module.cs when filled. " +
            "Left empty, the current token is kept.\n" +
            "Adjust S2S Token replaces ADJUST_S2S_TOKEN in Module.cs when filled. " +
            "Left empty, the current token is kept.\n" +
            "Paste PNG from Clipboard replaces Assets/Art/Textures/game_icon.png, scaled to 512x512.",
            MessageType.None);
        _appName = EditorGUILayout.TextField("App Name", _appName);
        _version = EditorGUILayout.TextField("Version", _version);
        _buildNumber = EditorGUILayout.TextField("Build Number", _buildNumber);
        _adjustToken = EditorGUILayout.TextField("Adjust Token", _adjustToken);
        _adjustS2sToken = EditorGUILayout.TextField("Adjust S2S Token", _adjustS2sToken);

        EditorGUILayout.LabelField("Game Icon", EditorStyles.boldLabel);
        if (GUILayout.Button("Paste PNG from Clipboard", GUILayout.Height(24f)))
            PasteGameIconFromClipboard();

        EditorGUILayout.LabelField("Custom Keystore", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _customKeystorePath = EditorGUILayout.TextField("Path", _customKeystorePath);
        if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
            BrowseKeystoreFile();
        EditorGUILayout.EndHorizontal();
        _keystorePassword = EditorGUILayout.PasswordField("Keystore Password", _keystorePassword);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Firebase google-services.json", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Paste google-services.json below, or load it from a .json file. " +
            "Apply updates push settings, Android package name, company name, and AndroidManifest.xml.",
            MessageType.Info);
        EditorGUILayout.LabelField("JSON File", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _jsonFilePath = EditorGUILayout.TextField("Path", _jsonFilePath);
        if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
            BrowseJsonFile();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Load File into Editor", GUILayout.Height(24f)))
            LoadJsonFromFile();
        if (GUILayout.Button("Apply from File", GUILayout.Height(24f)))
            ApplyFromFile();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("JSON Text", EditorStyles.boldLabel);
        _jsonScroll = EditorGUILayout.BeginScrollView(_jsonScroll, GUILayout.ExpandHeight(true));
        _jsonText = EditorGUILayout.TextArea(_jsonText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear", GUILayout.Width(60f)))
        {
            _jsonText = string.Empty;
            _jsonFilePath = string.Empty;
            _statusMessage = string.Empty;
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Apply to Push Settings", GUILayout.Width(160f), GUILayout.Height(28f)))
            ApplyJson();
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_statusMessage))
            EditorGUILayout.HelpBox(_statusMessage, MessageType.None);
    }

    void BrowseJsonFile()
    {
        var startDirectory = EditorPrefs.GetString(LastJsonDirectoryPref, Application.dataPath);
        var selectedPath = EditorUtility.OpenFilePanel(
            "Select google-services.json",
            startDirectory,
            "json");

        if (string.IsNullOrEmpty(selectedPath))
            return;

        _jsonFilePath = selectedPath;
        var directory = Path.GetDirectoryName(selectedPath);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastJsonDirectoryPref, directory);
    }

    void BrowseKeystoreFile()
    {
        var startDirectory = EditorPrefs.GetString(LastKeystoreDirectoryPref, GetProjectRoot());
        var selectedPath = EditorUtility.OpenFilePanel(
            "Select keystore",
            startDirectory,
            "keystore");

        if (string.IsNullOrEmpty(selectedPath))
            return;

        _customKeystorePath = selectedPath;
        var directory = Path.GetDirectoryName(selectedPath);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastKeystoreDirectoryPref, directory);
    }

    void LoadJsonFromFile()
    {
        if (!TryReadJsonFile(out var json, out var error))
        {
            _statusMessage = error;
            return;
        }

        _jsonText = json;
        _statusMessage = $"Loaded JSON from file:\n{_jsonFilePath}";
    }

    void ApplyFromFile()
    {
        if (!TryReadJsonFile(out var json, out var error))
        {
            _statusMessage = error;
            return;
        }

        _jsonText = json;
        ApplyJsonContent(json, $"Updated from file:\n{_jsonFilePath}");
    }

    bool TryReadJsonFile(out string json, out string error)
    {
        json = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(_jsonFilePath))
        {
            BrowseJsonFile();
            if (string.IsNullOrWhiteSpace(_jsonFilePath))
            {
                error = "No JSON file selected.";
                return false;
            }
        }

        if (!File.Exists(_jsonFilePath))
        {
            error = $"File not found:\n{_jsonFilePath}";
            return false;
        }

        try
        {
            json = File.ReadAllText(_jsonFilePath);
            return true;
        }
        catch (Exception ex)
        {
            error = "Failed to read JSON file: " + ex.Message;
            return false;
        }
    }

    void ApplyJson()
    {
        ApplyJsonContent(_jsonText, null);
    }

    void ApplyJsonContent(string json, string successPrefix)
    {
        try
        {
            var details = string.Empty;
            var companyName = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                details = "JSON empty; push notifications data not updated.\n";
            }
            else
            {
                if (!GoogleServicesJsonParser.TryParse(json, out var parsed, out var parseError))
                {
                    _statusMessage = parseError;
                    return;
                }

                if (!TryUpdateSettingsAsset(parsed, out var updateError))
                {
                    _statusMessage = updateError;
                    return;
                }

                companyName = parsed.CompanyName;
                var projectNotes = TryUpdateProjectIdentifiers(parsed);
                var manifestNotes = TryUpdateAndroidManifestPackage(parsed.PackageName);

                details =
                    "Updated pushNotificationsSettings:\n" +
                    $"firebaseProjectNumber = {parsed.ProjectNumber}\n" +
                    $"firebaseProjectID = {parsed.ProjectId}\n" +
                    $"firebaseAppID = {parsed.AppId}\n" +
                    $"firebaseWebApiKey = {parsed.ApiKey}\n" +
                    $"Android package = {parsed.PackageName}\n" +
                    $"companyName = {parsed.CompanyName}\n" +
                    projectNotes + manifestNotes;
            }

            var appNameNotes = TryUpdateAppName(_appName);
            var versionNotes = TryUpdateVersion(_version);
            var buildNumberNotes = TryUpdateBuildNumber(_buildNumber);
            var adjustTokenNotes = TryUpdateAdjustToken(_adjustToken);
            var adjustS2sTokenNotes = TryUpdateAdjustS2sToken(_adjustS2sToken);
            string keystoreFileName = null;
            string keystoreNotes;
            if (!string.IsNullOrWhiteSpace(_customKeystorePath))
            {
                keystoreNotes = TryImportCustomKeystore(
                    _customKeystorePath,
                    _keystorePassword,
                    out keystoreFileName);
            }
            else
            {
                keystoreNotes = TryCreateKeystore(_keystorePassword, companyName);
                if (!string.IsNullOrWhiteSpace(_keystorePassword))
                    keystoreFileName = PushNotificationsProjectSigningStore.KeystoreFileName;
            }

            PushNotificationsProjectSigningStore.Save(
                _keystorePassword,
                keystoreFileName,
                string.IsNullOrWhiteSpace(keystoreFileName)
                    ? null
                    : PlayerSettings.Android.keyaliasName);

            details += appNameNotes + versionNotes + buildNumberNotes + adjustTokenNotes + adjustS2sTokenNotes + keystoreNotes;

            _statusMessage = string.IsNullOrEmpty(successPrefix)
                ? details
                : successPrefix + "\n\n" + details;
        }
        catch (Exception ex)
        {
            _statusMessage = "Import failed.";
            Debug.LogException(ex);
        }
    }

    static bool TryUpdateSettingsAsset(GoogleServicesJsonParser.ParsedSettings parsed, out string error)
    {
        error = string.Empty;

        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsAssetPath);
        if (asset == null)
        {
            error = $"Asset not found at {SettingsAssetPath}.";
            return false;
        }

        var serializedObject = new SerializedObject(asset);
        SetString(serializedObject, "firebaseWebApiKey", parsed.ApiKey);
        SetString(serializedObject, "firebaseProjectNumber", parsed.ProjectNumber);
        SetString(serializedObject, "firebaseAppID", parsed.AppId);
        SetString(serializedObject, "firebaseProjectID", parsed.ProjectId);
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return true;
    }

    static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Property '{propertyName}' was not found on {SettingsAssetPath}.");

        property.stringValue = value ?? string.Empty;
    }

    static string TryUpdateProjectIdentifiers(GoogleServicesJsonParser.ParsedSettings parsed)
    {
        if (string.IsNullOrWhiteSpace(parsed.PackageName) && string.IsNullOrWhiteSpace(parsed.CompanyName))
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(parsed.PackageName))
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, parsed.PackageName);

        if (!string.IsNullOrWhiteSpace(parsed.CompanyName))
            PlayerSettings.companyName = parsed.CompanyName;

        return "Updated Player Settings (Android bundle identifier and company name).\n";
    }

    static string TryUpdateAndroidManifestPackage(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return "AndroidManifest.xml skipped (no package name in JSON).\n";

        var manifestPath = Path.Combine(GetProjectRoot(), AndroidManifestPath);
        if (!File.Exists(manifestPath))
            return "AndroidManifest.xml not found, skipped.\n";

        var manifestText = File.ReadAllText(manifestPath);
        if (!Regex.IsMatch(manifestText, @"\bpackage\s*=\s*""[^""]*"""))
            return "AndroidManifest.xml has no package attribute, skipped.\n";

        var updatedText = Regex.Replace(
            manifestText,
            @"\bpackage\s*=\s*""[^""]*""",
            _ => $"package=\"{packageName}\"");

        if (updatedText == manifestText)
            return "AndroidManifest.xml package attribute was not changed.\n";

        File.WriteAllText(manifestPath, updatedText);
        AssetDatabase.Refresh();
        return $"Updated AndroidManifest.xml package to {packageName}.\n";
    }

    static string TryUpdateAdjustToken(string adjustToken)
    {
        if (string.IsNullOrWhiteSpace(adjustToken))
            return "Adjust token empty; Module APP_TOKEN not changed.\n";

        var trimmed = adjustToken.Trim();
        var scriptPath = FindModuleProjectPath();
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            return "Module.cs not found; APP_TOKEN not changed.\n";

        var scriptText = File.ReadAllText(scriptPath);
        var pattern = new Regex(
            @"private\s+static\s+readonly\s+string\s+APP_TOKEN\s*=\s*""[^""]*""\s*;");
        var match = pattern.Match(scriptText);
        if (!match.Success)
            return "Module.cs has no private static readonly string APP_TOKEN assignment; not changed.\n";

        var escapedToken = trimmed.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var replacement = Regex.Replace(
            match.Value,
            @"""[^""]*""",
            $"\"{escapedToken}\"");

        if (string.Equals(match.Value, replacement, StringComparison.Ordinal))
            return "Adjust token unchanged; Module APP_TOKEN not replaced.\n";

        var updatedText = scriptText.Substring(0, match.Index) + replacement + scriptText.Substring(match.Index + match.Length);
        File.WriteAllText(scriptPath, updatedText);
        return $"Updated Module APP_TOKEN = {trimmed}\n";
    }

    static string TryUpdateAdjustS2sToken(string adjustS2sToken)
    {
        if (string.IsNullOrWhiteSpace(adjustS2sToken))
            return "Adjust S2S token empty; Module ADJUST_S2S_TOKEN not changed.\n";

        var trimmed = adjustS2sToken.Trim();
        var scriptPath = FindModuleProjectPath();
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            return "Module.cs not found; ADJUST_S2S_TOKEN not changed.\n";

        var scriptText = File.ReadAllText(scriptPath);
        var pattern = new Regex(
            @"private\s+static\s+readonly\s+string\s+ADJUST_S2S_TOKEN\s*=\s*""[^""]*""\s*;");
        var match = pattern.Match(scriptText);
        if (!match.Success)
            return "Module.cs has no private static readonly string ADJUST_S2S_TOKEN assignment; not changed.\n";

        var escapedToken = trimmed.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var replacement = Regex.Replace(
            match.Value,
            @"""[^""]*""",
            $"\"{escapedToken}\"");

        if (string.Equals(match.Value, replacement, StringComparison.Ordinal))
            return "Adjust S2S token unchanged; Module ADJUST_S2S_TOKEN not replaced.\n";

        var updatedText = scriptText.Substring(0, match.Index) + replacement + scriptText.Substring(match.Index + match.Length);
        File.WriteAllText(scriptPath, updatedText);
        return $"Updated Module ADJUST_S2S_TOKEN = {trimmed}\n";
    }

    static string FindModuleProjectPath()
    {
        var path = Path.Combine(GetProjectRoot(), "Module", "Project", "Module.cs");
        return File.Exists(path) ? path : null;
    }

    static string TryUpdateAppName(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return "App name empty; productName not changed.\n";

        var trimmed = appName.Trim();
        if (string.Equals(PlayerSettings.productName, trimmed, StringComparison.Ordinal))
            return "App name unchanged; productName not replaced.\n";

        PlayerSettings.productName = trimmed;
        return $"Updated productName = {trimmed}\n";
    }

    static string TryUpdateVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "Version empty; bundleVersion not changed.\n";

        var trimmed = version.Trim();
        if (string.Equals(PlayerSettings.bundleVersion, trimmed, StringComparison.Ordinal))
            return "Version unchanged; bundleVersion not replaced.\n";

        PlayerSettings.bundleVersion = trimmed;
        return $"Updated bundleVersion = {trimmed}\n";
    }

    static string TryUpdateBuildNumber(string buildNumber)
    {
        if (string.IsNullOrWhiteSpace(buildNumber))
            return "Build number empty; Android bundleVersionCode not changed.\n";

        var trimmed = buildNumber.Trim();
        if (!int.TryParse(trimmed, out var parsed) || parsed < 0)
            return $"Build number '{trimmed}' is not a non-negative integer; Android bundleVersionCode not changed.\n";

        if (PlayerSettings.Android.bundleVersionCode == parsed)
            return "Build number unchanged; Android bundleVersionCode not replaced.\n";

        PlayerSettings.Android.bundleVersionCode = parsed;
        return $"Updated Android bundleVersionCode = {parsed}\n";
    }

    static string TryImportCustomKeystore(string sourceKeystorePath, string password, out string importedFileName)
    {
        importedFileName = null;

        if (string.IsNullOrWhiteSpace(sourceKeystorePath))
            return "Custom keystore path empty; current keystore settings kept.\n";

        if (string.IsNullOrWhiteSpace(password))
            return "Custom keystore selected but password is empty; keystore not imported.\n";

        var trimmedPath = sourceKeystorePath.Trim();
        if (!File.Exists(trimmedPath))
            return $"Custom keystore not found:\n{trimmedPath}\n";

        var trimmedPassword = password.Trim();
        var fileName = Path.GetFileName(trimmedPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return "Custom keystore file name is invalid.\n";

        var projectRoot = GetProjectRoot();
        var destinationPath = Path.Combine(projectRoot, fileName);

        try
        {
            var fullSource = Path.GetFullPath(trimmedPath);
            var fullDestination = Path.GetFullPath(destinationPath);
            if (!string.Equals(fullSource, fullDestination, StringComparison.OrdinalIgnoreCase))
                File.Copy(fullSource, fullDestination, overwrite: true);
        }
        catch (Exception ex)
        {
            return "Failed to copy custom keystore: " + ex.Message + "\n";
        }

        importedFileName = fileName;
        if (!PushNotificationsProjectSigningStore.TryConfigureAndroidKeystoreSettings(
                trimmedPassword,
                destinationPath,
                out var configureError))
            return configureError;

        AssetDatabase.Refresh();
        return
            $"Imported custom keystore as {fileName} at project root " +
            $"(alias '{PlayerSettings.Android.keyaliasName}') and configured Android signing.\n";
    }

    static string TryCreateKeystore(string password, string companyName)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Keystore password empty; current keystore settings kept.\n";

        var trimmedPassword = password.Trim();
        var projectRoot = GetProjectRoot();
        var keystorePath = Path.Combine(projectRoot, PushNotificationsProjectSigningStore.KeystoreFileName);
        var currentPassword = PushNotificationsProjectSigningStore.Load().KeystorePassword;
        var passwordUnchanged = string.Equals(currentPassword, trimmedPassword, StringComparison.Ordinal);

        if (File.Exists(keystorePath))
        {
            if (passwordUnchanged)
                return "Keystore password unchanged; current keystore settings kept.\n";

            if (!PushNotificationsProjectSigningStore.TryConfigureAndroidKeystoreSettings(
                    trimmedPassword,
                    keystorePath,
                    out var updateError))
                return updateError;

            return $"{PushNotificationsProjectSigningStore.KeystoreFileName} already exists at project root; updated Android signing settings.\n";
        }

        if (ProjectKeystoreAlreadyConfigured(projectRoot))
            return "Project already has a keystore configured, skipped creation.\n";

        var keytoolPath = PushNotificationsProjectSigningStore.FindKeytoolPath();
        if (string.IsNullOrEmpty(keytoolPath))
            return "keytool not found. Install Android Build Support or set JAVA_HOME.\n";

        var dname = BuildKeystoreDistinguishedName(companyName);
        var arguments =
            "-genkeypair -v " +
            $"-keystore \"{keystorePath}\" " +
            $"-alias {PushNotificationsProjectSigningStore.KeystoreAlias} " +
            "-keyalg RSA -keysize 2048 -validity 10000 " +
            $"-storepass \"{PushNotificationsProjectSigningStore.EscapeCmdArgument(trimmedPassword)}\" " +
            $"-keypass \"{PushNotificationsProjectSigningStore.EscapeCmdArgument(trimmedPassword)}\" " +
            $"-dname \"{PushNotificationsProjectSigningStore.EscapeCmdArgument(dname)}\"";

        if (!PushNotificationsProjectSigningStore.TryRunProcess(keytoolPath, arguments, out _, out var processError))
            return "Failed to create keystore: " + processError + "\n";

        if (!PushNotificationsProjectSigningStore.TryConfigureAndroidKeystoreSettings(
                trimmedPassword,
                keystorePath,
                out var configureError))
            return configureError;

        AssetDatabase.Refresh();
        return $"Created {PushNotificationsProjectSigningStore.KeystoreFileName} at project root and configured Android signing.\n";
    }

    static bool ProjectKeystoreAlreadyConfigured(string projectRoot)
    {
        if (!PlayerSettings.Android.useCustomKeystore)
            return false;

        var configuredPath = PushNotificationsProjectSigningStore.ResolveKeystorePath(
            PlayerSettings.Android.keystoreName,
            projectRoot);
        return !string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath);
    }

    static string BuildKeystoreDistinguishedName(string companyName)
    {
        var cn = string.IsNullOrWhiteSpace(companyName) ? "Android App" : companyName.Trim();
        return $"CN={cn}, OU=Development, O={cn}, L=Unknown, ST=Unknown, C=US";
    }

    void PasteGameIconFromClipboard()
    {
        if (!WindowsClipboardImage.TryReadTexture(out var source, out var error))
        {
            _statusMessage = error;
            return;
        }

        Texture2D scaled = null;
        try
        {
            scaled = ScaleTexture(source, GameIconSize, GameIconSize);
            var pngBytes = scaled.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                _statusMessage = "Failed to encode game icon as PNG.";
                return;
            }

            var fullPath = Path.Combine(GetProjectRoot(), GameIconAssetPath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(GameIconAssetPath, ImportAssetOptions.ForceUpdate);
            _statusMessage =
                $"Replaced {GameIconAssetPath} with clipboard PNG scaled to {GameIconSize}x{GameIconSize}.";
        }
        catch (Exception ex)
        {
            _statusMessage = "Failed to write game icon from clipboard.";
            Debug.LogException(ex);
        }
        finally
        {
            if (source != null)
                UnityEngine.Object.DestroyImmediate(source);
            if (scaled != null)
                UnityEngine.Object.DestroyImmediate(scaled);
        }
    }

    static Texture2D ScaleTexture(Texture2D source, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        var previousFilter = source.filterMode;
        source.filterMode = FilterMode.Bilinear;
        var previousActive = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var dest = new Texture2D(width, height, TextureFormat.RGBA32, false);
        dest.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        dest.Apply();
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(rt);
        source.filterMode = previousFilter;
        return dest;
    }

    static string GetProjectRoot() =>
        PushNotificationsProjectSigningStore.GetProjectRoot();
}

static class WindowsClipboardImage
{
    const uint CfDib = 8;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern UIntPtr GlobalSize(IntPtr hMem);

    public static bool TryReadTexture(out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        if (!OpenClipboard(IntPtr.Zero))
        {
            error = "Could not open the Windows clipboard.";
            return false;
        }

        byte[] pngBytes = null;
        byte[] dibBytes = null;
        try
        {
            var pngFormat = RegisterClipboardFormat("PNG");
            if (pngFormat != 0 && IsClipboardFormatAvailable(pngFormat))
                TryReadClipboardBytes(pngFormat, out pngBytes);

            if ((pngBytes == null || pngBytes.Length == 0) && IsClipboardFormatAvailable(CfDib))
                TryReadClipboardBytes(CfDib, out dibBytes);
        }
        finally
        {
            CloseClipboard();
        }

        if (pngBytes != null && pngBytes.Length > 0)
        {
            var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (loaded.LoadImage(pngBytes))
            {
                texture = loaded;
                return true;
            }

            UnityEngine.Object.DestroyImmediate(loaded);
        }

        if (dibBytes != null && dibBytes.Length > 0 && TryCreateTextureFromDib(dibBytes, out texture, out error))
            return true;

        if (string.IsNullOrEmpty(error))
            error = "Clipboard has no PNG or bitmap image. Copy a PNG image and try again.";
        return false;
    }

    static bool TryReadClipboardBytes(uint format, out byte[] data)
    {
        data = null;
        var handle = GetClipboardData(format);
        if (handle == IntPtr.Zero)
            return false;

        var locked = GlobalLock(handle);
        if (locked == IntPtr.Zero)
            return false;

        try
        {
            var size = (int)GlobalSize(handle).ToUInt32();
            if (size <= 0)
                return false;

            data = new byte[size];
            Marshal.Copy(locked, data, 0, size);
            return true;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    static bool TryCreateTextureFromDib(byte[] dib, out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        if (dib == null || dib.Length < 40)
        {
            error = "Clipboard bitmap data is too small.";
            return false;
        }

        var headerSize = BitConverter.ToInt32(dib, 0);
        var width = BitConverter.ToInt32(dib, 4);
        var heightRaw = BitConverter.ToInt32(dib, 8);
        var bitCount = BitConverter.ToInt16(dib, 14);
        var compression = BitConverter.ToInt32(dib, 16);
        var topDown = heightRaw < 0;
        var height = Math.Abs(heightRaw);

        if (width <= 0 || height <= 0)
        {
            error = "Clipboard bitmap has an invalid size.";
            return false;
        }

        if (bitCount != 24 && bitCount != 32)
        {
            error = $"Clipboard bitmap format ({bitCount} bpp) is not supported. Copy a PNG instead.";
            return false;
        }

        if (compression != 0 && compression != 3)
        {
            error = "Clipboard bitmap compression is not supported. Copy a PNG instead.";
            return false;
        }

        var pixelOffset = headerSize;
        if (compression == 3)
            pixelOffset += 12;

        var stride = ((width * bitCount + 31) / 32) * 4;
        if (pixelOffset + stride * height > dib.Length)
        {
            error = "Clipboard bitmap pixel data is incomplete.";
            return false;
        }

        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            var srcY = topDown ? y : height - 1 - y;
            var rowStart = pixelOffset + srcY * stride;
            for (var x = 0; x < width; x++)
            {
                var i = rowStart + x * (bitCount / 8);
                var b = dib[i];
                var g = dib[i + 1];
                var r = dib[i + 2];
                var a = bitCount == 32 ? dib[i + 3] : (byte)255;
                pixels[y * width + x] = new Color32(r, g, b, a);
            }
        }

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        return true;
    }
}

[InitializeOnLoad]
static class PushNotificationsProjectSigningAutoRestore
{
    static PushNotificationsProjectSigningAutoRestore()
    {
        EditorApplication.delayCall += PushNotificationsProjectSigningStore.TryRestorePlayerSettings;
    }
}

static class PushNotificationsProjectSigningStore
{
    const string PrefsPrefix = "PushNotificationsSettingsImporter_";
    const string SigningSettingsFileName = "editor-android-signing.json";

    public const string KeystoreFileName = "release.keystore";
    public const string KeystoreAlias = "release";

    [Serializable]
    sealed class SigningSettingsFile
    {
        public string keystorePassword;
        public string keystoreFileName;
        public string keystoreAlias;
    }

    public readonly struct SigningSettingsData
    {
        public readonly string KeystorePassword;
        public readonly string KeystoreFileName;
        public readonly string KeystoreAliasName;

        public SigningSettingsData(
            string keystorePassword,
            string keystoreFileName = null,
            string keystoreAlias = null)
        {
            KeystorePassword = keystorePassword ?? string.Empty;
            KeystoreFileName = string.IsNullOrWhiteSpace(keystoreFileName)
                ? string.Empty
                : Path.GetFileName(keystoreFileName.Trim());
            KeystoreAliasName = keystoreAlias?.Trim() ?? string.Empty;
        }
    }

    public static SigningSettingsData Load()
    {
        var keystorePassword = EditorPrefs.GetString(GetPrefsKey("KeystorePassword"), string.Empty);
        var keystoreFileName = EditorPrefs.GetString(GetPrefsKey("KeystoreFileName"), string.Empty);
        var keystoreAlias = EditorPrefs.GetString(GetPrefsKey("KeystoreAlias"), string.Empty);

        var filePath = GetSigningSettingsFilePath();
        if (!File.Exists(filePath))
            return new SigningSettingsData(keystorePassword, keystoreFileName, keystoreAlias);

        try
        {
            var json = File.ReadAllText(filePath);
            var fileData = JsonUtility.FromJson<SigningSettingsFile>(json);
            if (fileData == null)
                return new SigningSettingsData(keystorePassword, keystoreFileName, keystoreAlias);

            if (!string.IsNullOrEmpty(fileData.keystorePassword))
                keystorePassword = fileData.keystorePassword;
            if (!string.IsNullOrEmpty(fileData.keystoreFileName))
                keystoreFileName = fileData.keystoreFileName;
            if (!string.IsNullOrEmpty(fileData.keystoreAlias))
                keystoreAlias = fileData.keystoreAlias;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to read " + SigningSettingsFileName + ": " + ex.Message);
        }

        return new SigningSettingsData(keystorePassword, keystoreFileName, keystoreAlias);
    }

    public static void Save(string keystorePassword, string keystoreFileName = null, string keystoreAlias = null)
    {
        if (string.IsNullOrWhiteSpace(keystorePassword))
            return;

        keystorePassword = keystorePassword.Trim();
        var current = Load();
        if (string.IsNullOrWhiteSpace(keystoreFileName))
            keystoreFileName = current.KeystoreFileName;
        if (string.IsNullOrWhiteSpace(keystoreFileName))
            keystoreFileName = KeystoreFileName;
        else
            keystoreFileName = Path.GetFileName(keystoreFileName.Trim());

        if (string.IsNullOrWhiteSpace(keystoreAlias))
            keystoreAlias = current.KeystoreAliasName;
        if (string.IsNullOrWhiteSpace(keystoreAlias))
            keystoreAlias = PlayerSettings.Android.keyaliasName;
        keystoreAlias = keystoreAlias?.Trim() ?? string.Empty;

        if (string.Equals(current.KeystorePassword, keystorePassword, StringComparison.Ordinal)
            && string.Equals(current.KeystoreFileName, keystoreFileName, StringComparison.Ordinal)
            && string.Equals(current.KeystoreAliasName, keystoreAlias, StringComparison.Ordinal))
            return;

        EditorPrefs.SetString(GetPrefsKey("KeystorePassword"), keystorePassword);
        EditorPrefs.SetString(GetPrefsKey("KeystoreFileName"), keystoreFileName);
        EditorPrefs.SetString(GetPrefsKey("KeystoreAlias"), keystoreAlias);

        try
        {
            var fileData = new SigningSettingsFile
            {
                keystorePassword = keystorePassword,
                keystoreFileName = keystoreFileName,
                keystoreAlias = keystoreAlias
            };
            File.WriteAllText(GetSigningSettingsFilePath(), JsonUtility.ToJson(fileData, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to write " + SigningSettingsFileName + ": " + ex.Message);
        }
    }

    public static void TryRestorePlayerSettings()
    {
        var stored = Load();
        if (string.IsNullOrWhiteSpace(stored.KeystorePassword))
            return;

        var keystoreFileName = string.IsNullOrWhiteSpace(stored.KeystoreFileName)
            ? KeystoreFileName
            : stored.KeystoreFileName;
        var keystorePath = Path.Combine(GetProjectRoot(), keystoreFileName);
        if (!File.Exists(keystorePath))
            return;

        if (!TryConfigureAndroidKeystoreSettings(
                stored.KeystorePassword,
                keystorePath,
                out var error,
                stored.KeystoreAliasName))
            Debug.LogWarning("[PushNotificationsSettingsImporter] Failed to restore keystore settings: " + error);
    }

    public static bool TryConfigureAndroidKeystoreSettings(
        string password,
        string projectRootKeystorePath,
        out string error,
        string preferredAlias = null)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            error = "Keystore password is empty.\n";
            return false;
        }

        if (string.IsNullOrWhiteSpace(projectRootKeystorePath) || !File.Exists(projectRootKeystorePath))
        {
            error = "Keystore file not found at project root.\n";
            return false;
        }

        var projectRoot = GetProjectRoot();
        var fullKeystorePath = Path.GetFullPath(projectRootKeystorePath);
        var fullProjectRoot = Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var keystoreDirectory = Path.GetFullPath(Path.GetDirectoryName(fullKeystorePath) ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(keystoreDirectory, fullProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Keystore must be located at the project root.\n";
            return false;
        }

        var keystoreFileName = Path.GetFileName(fullKeystorePath);
        if (string.IsNullOrWhiteSpace(keystoreFileName))
        {
            error = "Keystore file name is invalid.\n";
            return false;
        }

        // Point Player Settings at the project-root copy (relative path = in-project).
        var relativeKeystorePath = keystoreFileName.Replace('\\', '/');

        string alias;
        if (!string.IsNullOrWhiteSpace(preferredAlias))
        {
            alias = preferredAlias.Trim();
        }
        else if (!TryReadFirstKeystoreAlias(fullKeystorePath, password.Trim(), out alias, out var aliasError))
        {
            error = aliasError;
            return false;
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = relativeKeystorePath;
        PlayerSettings.Android.keystorePass = password.Trim();
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = password.Trim();
        return true;
    }

    public static bool TryReadFirstKeystoreAlias(
        string keystorePath,
        string password,
        out string alias,
        out string error)
    {
        alias = string.Empty;
        error = string.Empty;

        var keytoolPath = FindKeytoolPath();
        if (string.IsNullOrEmpty(keytoolPath))
        {
            error = "keytool not found. Install Android Build Support or set JAVA_HOME.\n";
            return false;
        }

        var arguments =
            "-list -v " +
            $"-keystore \"{keystorePath}\" " +
            $"-storepass \"{EscapeCmdArgument(password)}\"";

        if (!TryRunProcess(keytoolPath, arguments, out var output, out var processError))
        {
            error = "Failed to read keystore aliases: " + processError + "\n";
            return false;
        }

        var match = Regex.Match(output ?? string.Empty, @"Alias name:\s*(.+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            // Non-verbose list format: "alias, date, PrivateKeyEntry,"
            match = Regex.Match(
                output ?? string.Empty,
                @"^([^\r\n,]+),\s*.*?PrivateKeyEntry",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            error = "No key alias found in keystore.\n";
            return false;
        }

        alias = match.Groups[1].Value.Trim();
        return true;
    }

    public static string EscapeCmdArgument(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\"", "\\\"");

    public static string FindKeytoolPath()
    {
        var jdkRoot = GetJdkRootPath();
        if (string.IsNullOrWhiteSpace(jdkRoot))
            return null;

        var keytoolName = Application.platform == RuntimePlatform.WindowsEditor ? "keytool.exe" : "keytool";
        var keytoolPath = Path.Combine(jdkRoot, "bin", keytoolName);
        return File.Exists(keytoolPath) ? keytoolPath : null;
    }

    static string GetJdkRootPath()
    {
        try
        {
            var settingsType = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor");
            var jdkRootProperty = settingsType?.GetProperty(
                "jdkRootPath",
                BindingFlags.Public | BindingFlags.Static);

            var jdkRoot = jdkRootProperty?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(jdkRoot))
                return jdkRoot;
        }
        catch
        {
            // Fall back to JAVA_HOME below.
        }

        return Environment.GetEnvironmentVariable("JAVA_HOME");
    }

    public static bool TryRunProcess(string fileName, string arguments, out string output, out string error)
    {
        output = string.Empty;
        error = string.Empty;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            output = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return true;

            error = string.IsNullOrWhiteSpace(stderr) ? output : stderr;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string ResolveKeystorePath(string keystoreName, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(keystoreName))
            return null;

        var relative = keystoreName.Trim();
        const string inProjectPrefix = "{inproject}:";
        while (relative.StartsWith(inProjectPrefix, StringComparison.Ordinal))
            relative = relative.Substring(inProjectPrefix.Length).Trim();

        return Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(projectRoot, relative);
    }

    public static string GetProjectRoot() =>
        Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

    static string GetSigningSettingsFilePath() =>
        Path.Combine(GetProjectRoot(), SigningSettingsFileName);

    static string GetPrefsKey(string suffix)
    {
        var projectId = (Application.dataPath ?? string.Empty).Replace('\\', '/');
        return PrefsPrefix + projectId + "_" + suffix;
    }
}

static class GoogleServicesJsonParser
{
    public readonly struct ParsedSettings
    {
        public readonly string ProjectNumber;
        public readonly string ProjectId;
        public readonly string AppId;
        public readonly string ApiKey;
        public readonly string PackageName;
        public readonly string CompanyName;

        public ParsedSettings(
            string projectNumber,
            string projectId,
            string appId,
            string apiKey,
            string packageName,
            string companyName)
        {
            ProjectNumber = projectNumber;
            ProjectId = projectId;
            AppId = appId;
            ApiKey = apiKey;
            PackageName = packageName;
            CompanyName = companyName;
        }
    }
    public static bool TryParse(string json, out ParsedSettings parsed, out string error)
    {
        parsed = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON input is empty.";
            return false;
        }

        GoogleServicesRoot root;
        try
        {
            root = JsonUtility.FromJson<GoogleServicesRoot>(json);
        }
        catch (Exception ex)
        {
            error = "Failed to parse JSON: " + ex.Message;
            return false;
        }

        if (root?.project_info == null)
        {
            error = "Missing project_info in JSON.";
            return false;
        }

        var projectNumber = root.project_info.project_number?.Trim();
        var projectId = root.project_info.project_id?.Trim();
        if (string.IsNullOrEmpty(projectNumber))
        {
            error = "Missing project_info.project_number.";
            return false;
        }

        if (string.IsNullOrEmpty(projectId))
        {
            error = "Missing project_info.project_id.";
            return false;
        }

        if (!TryGetClientValues(root, out var appId, out var apiKey, out var packageName, out error))
            return false;

        parsed = new ParsedSettings(
            projectNumber,
            projectId,
            appId,
            apiKey,
            packageName,
            DeriveCompanyNameFromPackage(packageName));
        return true;
    }

    public static string DeriveCompanyNameFromPackage(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return string.Empty;

        var parts = packageName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? parts[0] : parts[1];
    }

    static bool TryGetClientValues(
        GoogleServicesRoot root,
        out string appId,
        out string apiKey,
        out string packageName,
        out string error)
    {
        appId = string.Empty;
        apiKey = string.Empty;
        packageName = string.Empty;
        error = string.Empty;
        if (root.client == null || root.client.Length == 0)
        {
            error = "Missing client array in JSON.";
            return false;
        }

        foreach (var client in root.client)
        {
            appId = client?.client_info?.mobilesdk_app_id?.Trim();
            packageName = client?.client_info?.android_client_info?.package_name?.Trim();
            if (string.IsNullOrEmpty(appId))
                continue;

            if (client.api_key != null)
            {
                foreach (var keyEntry in client.api_key)
                {
                    var currentKey = keyEntry?.current_key?.Trim();
                    if (!string.IsNullOrEmpty(currentKey))
                    {
                        apiKey = currentKey;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(apiKey))
                return true;
        }
        if (string.IsNullOrEmpty(appId))
        {
            error = "Missing client_info.mobilesdk_app_id.";
            return false;
        }

        error = "Missing api_key.current_key.";
        return false;
    }

    [Serializable]
    sealed class GoogleServicesRoot
    {
        public ProjectInfo project_info;
        public ClientEntry[] client;
    }

    [Serializable]
    sealed class ProjectInfo
    {
        public string project_number;
        public string project_id;
    }

    [Serializable]
    sealed class ClientEntry
    {
        public ClientInfo client_info;
        public ApiKeyEntry[] api_key;
    }

    [Serializable]
    sealed class ClientInfo
    {
        public string mobilesdk_app_id;
        public AndroidClientInfo android_client_info;
    }

    [Serializable]
    sealed class AndroidClientInfo
    {
        public string package_name;
    }
    [Serializable]
    sealed class ApiKeyEntry
    {
        public string current_key;
    }
}
#endif
