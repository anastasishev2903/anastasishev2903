#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;

public static class TemplateCloudEnvironment
{
    const string ProductionEnvironmentName = "production";
    const int MaxAttempts = 30;
    const int RetryDelayMs = 1000;

    static bool s_IsSelecting;
    static int s_SelectVersion;

    public static void EnsureProductionIfCloudLinked()
    {
        if (!IsCloudProjectLinked())
            return;

        // Run on the next editor tick so EnvironmentProvider can finish
        // clearing stale env state when the cloud project id changes.
        EditorApplication.delayCall += StartSelection;
    }

    static void StartSelection()
    {
        if (s_IsSelecting)
            return;

        if (!IsCloudProjectLinked())
            return;

        var version = ++s_SelectVersion;
        EnsureProductionAsync(version);
    }

    static bool IsCloudProjectLinked()
    {
        try
        {
            return CloudProjectSettings.projectBound
                   && !string.IsNullOrWhiteSpace(CloudProjectSettings.projectId);
        }
        catch
        {
            return false;
        }
    }

    static async void EnsureProductionAsync(int version)
    {
        s_IsSelecting = true;
        try
        {
            // Give Unity Services time to finish bind/token setup.
            await Task.Delay(RetryDelayMs);

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (version != s_SelectVersion)
                    return;

                if (!IsCloudProjectLinked())
                    return;

                if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
                {
                    await Task.Delay(RetryDelayMs);
                    continue;
                }

                IEnvironmentsApi api;
                try
                {
                    api = EnvironmentsApi.Instance;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[TemplateAssetPostprocessor] Environments API not ready: {ex.Message}");
                    await Task.Delay(RetryDelayMs);
                    continue;
                }

                await api.RefreshAsync();

                if (api.Environments == null || api.Environments.Count == 0)
                {
                    await Task.Delay(RetryDelayMs);
                    continue;
                }

                if (!TryFindProductionEnvironment(api, out var production))
                {
                    Debug.LogWarning(
                        "[TemplateAssetPostprocessor] Linked Unity Cloud project has no production environment.");
                    return;
                }

                var alreadySelected = api.ActiveEnvironmentId == production.Id
                    && string.Equals(
                        api.ActiveEnvironmentName,
                        production.Name,
                        StringComparison.OrdinalIgnoreCase);

                if (!alreadySelected)
                {
                    api.SetActiveEnvironment(production);
                    Debug.Log(
                        $"[TemplateAssetPostprocessor] Services environment set to '{production.Name}' ({production.Id}).");
                }

                // Re-check after a beat: project-id trackers can clear the env after we set it.
                await Task.Delay(RetryDelayMs);
                if (version != s_SelectVersion)
                    return;

                api = EnvironmentsApi.Instance;
                if (api.ActiveEnvironmentId == production.Id
                    && string.Equals(
                        api.ActiveEnvironmentName,
                        production.Name,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                api.SetActiveEnvironment(production);
                Debug.Log(
                    $"[TemplateAssetPostprocessor] Services environment re-applied to '{production.Name}' ({production.Id}).");
                return;
            }

            Debug.LogWarning(
                "[TemplateAssetPostprocessor] Could not set production environment: environments were not available in time.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TemplateAssetPostprocessor] Could not set production environment: {ex.Message}");
        }
        finally
        {
            if (version == s_SelectVersion)
                s_IsSelecting = false;
        }
    }

    static bool TryFindProductionEnvironment(IEnvironmentsApi api, out EnvironmentInfo production)
    {
        production = default;
        EnvironmentInfo? namedProduction = null;
        EnvironmentInfo? defaultEnvironment = null;

        foreach (var environment in api.Environments)
        {
            if (string.Equals(environment.Name, ProductionEnvironmentName, StringComparison.OrdinalIgnoreCase))
                namedProduction = environment;

            if (environment.IsDefault)
                defaultEnvironment = environment;
        }

        if (namedProduction.HasValue)
        {
            production = namedProduction.Value;
            return true;
        }

        if (defaultEnvironment.HasValue)
        {
            production = defaultEnvironment.Value;
            return true;
        }

        return false;
    }
}
#endif
