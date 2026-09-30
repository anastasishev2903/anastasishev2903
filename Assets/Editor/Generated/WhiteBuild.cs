#if UNITY_EDITOR
// WhiteBuild.cs — batchmode Android build entry for the WHITE (game-only) build.
//
// ADDED file (dropped into Assets/Editor/Generated/ by the pipeline). It is NOT
// part of the template and NOT shipped in the APK (Editor-only). It does not
// modify any template code — CLAUDE-unity.md §00.
//
// Codemagic runs (see codemagic-unity.yaml):
//   Unity -batchmode -quit -projectPath . -executeMethod WhiteBuild.BuildAab
//   Unity -batchmode -quit -projectPath . -executeMethod WhiteBuild.BuildTestApk
//
// Output paths / signing come from env vars so Codemagic controls them:
//   WB_OUTPUT      absolute output file path (…/app.aab or …/app.apk)
//   WB_KEYSTORE    keystore path        WB_KEYSTORE_PASS  store password
//   WB_KEY_ALIAS   key alias            WB_KEY_PASS       key password
//
// B_LOGS: production (AAB) strips it; the test APK keeps it. The define lives in
// Assets/csc.rsp; here we also set it in PlayerSettings scripting defines so the
// batchmode compile matches, without editing csc.rsp from C#.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class WhiteBuild
{
    static string[] EnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
    }

    static string Env(string k, string fallback = "") =>
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k)) ? fallback : Environment.GetEnvironmentVariable(k);

    static void ApplySigning()
    {
        var ks = Env("WB_KEYSTORE");
        if (string.IsNullOrEmpty(ks)) return;
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ks;
        PlayerSettings.Android.keystorePass = Env("WB_KEYSTORE_PASS");
        PlayerSettings.Android.keyaliasName = Env("WB_KEY_ALIAS");
        PlayerSettings.Android.keyaliasPass = Env("WB_KEY_PASS");
    }

    // ALL builds ship LZ4HC-compressed Unity data (AAB and both APKs).
    //
    // The compression method is NOT a project setting — it lives in
    // EditorUserBuildSettings, i.e. Library/EditorUserBuildSettings.asset, which
    // is git-ignored and never reaches the builder. So it cannot be baked into
    // ProjectSettings.asset and has to be set from editor code ON the machine
    // that builds. Two belts, because they cover different build paths:
    //   - the Build Settings "Compression Method" value, which is what a
    //     UBA-driven Editor build uses (it never calls BuildAab/BuildTestApk);
    //   - BuildOptions.CompressWithLz4HC on our own BuildPlayer call.
    // Lz4HC (not Lz4) is the high-compression variant: slower to build, smaller
    // download, same runtime decompression cost — the right trade for a release.
    //
    // REFLECTION IS DELIBERATE. EditorUserBuildSettings.SetCompressionType and
    // its Compression enum are internal in several Unity versions (the known
    // recipe calls them with BindingFlags.NonPublic). A direct call would be a
    // COMPILE error on a version where they are internal, and since this file is
    // compiled by the Editor on the builder, that would take down every build —
    // not just lose the setting. Reflection degrades to a warning instead.
    static void ApplyCompression()
    {
        try
        {
            var m = typeof(EditorUserBuildSettings).GetMethod("SetCompressionType",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (m == null)
            {
                Debug.LogWarning("[WhiteBuild] SetCompressionType not found on this Unity version; "
                                 + "relying on BuildOptions.CompressWithLz4HC only");
                return;
            }
            var ps = m.GetParameters();
            if (ps.Length != 2 || !ps[1].ParameterType.IsEnum)
            {
                Debug.LogWarning("[WhiteBuild] unexpected SetCompressionType signature; skipping");
                return;
            }
            object lz4hc = Enum.Parse(ps[1].ParameterType, "Lz4HC", true);
            m.Invoke(null, new object[] { BuildTargetGroup.Android, lz4hc });
            Debug.Log("[WhiteBuild] compression method = Lz4HC");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WhiteBuild] could not set Lz4HC compression: " + e.Message);
        }
    }

    // Debug Symbols (the symbols.zip next to the artifact) OFF for every build.
    // Same story as compression: it is an EditorUserBuildSettings value, so it
    // cannot travel in the repo, and the enum/property has moved between Unity
    // versions — hence reflection rather than a hard reference.
    static void DisableDebugSymbols()
    {
        try
        {
            var p = typeof(EditorUserBuildSettings).GetProperty("androidCreateSymbols",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p == null || !p.PropertyType.IsEnum)
            {
                Debug.LogWarning("[WhiteBuild] androidCreateSymbols not available; debug symbols left at default");
                return;
            }
            p.SetValue(null, Enum.Parse(p.PropertyType, "Disabled", true), null);
            Debug.Log("[WhiteBuild] debug symbols = Disabled");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WhiteBuild] could not disable debug symbols: " + e.Message);
        }
    }

    // UBA (Unity Build Automation) drives the Editor's own build path and never
    // calls BuildAab/BuildTestApk, so anything we need applied there has to run
    // from its preExportMethod hook. Wired by uba-prepare.sh as
    // settings.advanced.unity.preExportMethod = "WhiteBuild.PreExport".
    // Must NOT call EditorApplication.Exit — the build continues after it.
    // The UBA hook for both build variants. Which variant THIS build is comes from
    // WB_LOGS / the .obf-logs project file. The two deliverables map like this:
    //   nologs variant → AAB (buildAppBundle ON): the Play AAB, and the pipeline
    //                    derives a universal APK-nologs from it via bundletool;
    //   logs   variant → APK (buildAppBundle OFF): the QA APK, delivered directly.
    // So the two builds produce DIFFERENT extensions (.aab vs .apk) — which also
    // keeps their downloads from colliding. Obfuscation is identical across both
    // (already baked in on the host; here it is skipped via the .preobfuscated marker).
    public static void PreExport()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        // Obfuscation is already baked into the pushed source (host gate); RunObfuscation
        // no-ops on the .preobfuscated marker. Signing is settled (UBA registered the
        // keystore before this build) — matching the required order: signing before obf.
        RunObfuscation();
        bool logs = ResolveLogsVariant();
        SetBLogs(logs);
        // nologs → AAB, logs → APK.
        EditorUserBuildSettings.buildAppBundle = !logs;
        ApplyCompression();
        DisableDebugSymbols();
        Debug.Log($"[WhiteBuild] PreExport applied (Lz4HC, no debug symbols, B_LOGS={logs}, buildAppBundle={!logs})");
    }

    // Explicit entry points, in case a build target is wired to a named method
    // instead of the env/file channel. Both just fix the logs state then defer.
    public static void PreExportNoLogs() { Environment.SetEnvironmentVariable("WB_LOGS", "0"); PreExport(); }
    public static void PreExportWithLogs() { Environment.SetEnvironmentVariable("WB_LOGS", "1"); PreExport(); }

    static bool ResolveLogsVariant()
    {
        string e = Env("WB_LOGS");
        if (e == "1" || e.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (e == "0" || e.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            string f = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ".obf-logs");
            if (File.Exists(f)) { string v = File.ReadAllText(f).Trim(); return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); }
        }
        catch { }
        return false; // default: production, logs stripped
    }

    // ObfuscateBuildHook is staged next to this file (Assets/Editor/Generated/), so
    // it is normally a direct reference. Call it by reflection so a project WITHOUT
    // the hook (or with obfuscation intentionally absent) still builds cleanly
    // instead of failing to compile WhiteBuild.
    static void RunObfuscation()
    {
        // The pipeline obfuscates on the HOST (the builder has no dotnet/SDK for
        // Plana) and pushes the already-obfuscated project with a .preobfuscated
        // marker. In that mode there is nothing to do here.
        try
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (File.Exists(Path.Combine(root, ".preobfuscated")))
            { Debug.Log("[WhiteBuild] source is pre-obfuscated on the host — skipping on-builder obfuscation"); return; }
        }
        catch { }
        if (Env("WB_OBFUSCATE") == "0") { Debug.Log("[WhiteBuild] WB_OBFUSCATE=0 — obfuscation skipped"); return; }
        try
        {
            var t = Type.GetType("ObfuscateBuildHook");
            var m = t?.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            if (m == null) { Debug.LogWarning("[WhiteBuild] ObfuscateBuildHook not present — building WITHOUT obfuscation"); return; }
            m.Invoke(null, null);
        }
        catch (Exception e) { Debug.LogError("[WhiteBuild] obfuscation failed (building anyway): " + e); }
    }

    static void SetBLogs(bool on)
    {
        var t = NamedBuildTarget.Android;
        var defines = PlayerSettings.GetScriptingDefineSymbols(t)
            .Split(';').Where(d => !string.IsNullOrWhiteSpace(d) && d != "B_LOGS").ToList();
        if (on) defines.Add("B_LOGS");
        PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defines));
    }

    static void Build(bool aab, bool bLogs)
    {
        var output = Env("WB_OUTPUT", aab ? "build/app.aab" : "build/app.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = aab;
        SetBLogs(bLogs);
        ApplySigning();
        ApplyCompression();
        DisableDebugSymbols();

        var opts = new BuildPlayerOptions
        {
            scenes = EnabledScenes(),
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.CompressWithLz4HC,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;
        Debug.Log($"[WhiteBuild] result={summary.result} size={summary.totalSize} out={output}");
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
    }

    // Production white build: AAB, no logs.
    public static void BuildAab() => Build(aab: true, bLogs: false);

    // QA build: installable APK with logs for the emulator screenshot gate.
    public static void BuildTestApk() => Build(aab: false, bLogs: true);
}
#endif
