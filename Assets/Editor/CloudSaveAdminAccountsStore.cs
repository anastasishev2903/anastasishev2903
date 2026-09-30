#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
sealed class CloudSaveAdminAccount
{
    public string id;
    public string title;
    public string keyId;
    public string secretKeyId;
    public string projectId;
    public string environmentId;
    public string adjustToken;
    public string adjustS2sToken;
}

[Serializable]
sealed class CloudSaveAdminAccountsFile
{
    public string selectedId;
    public CloudSaveAdminAccount[] accounts;
}

static class CloudSaveAdminAccountsStore
{
    public const string FileName = "cloud-save-admin-accounts.json";

    public static string GetFilePath()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        return Path.Combine(projectRoot, FileName);
    }

    public static CloudSaveAdminAccountsFile Load(
        string fallbackKeyId,
        string fallbackSecretKeyId,
        string fallbackProjectId,
        string fallbackEnvironmentId)
    {
        var file = TryRead();
        if (file?.accounts != null && file.accounts.Length > 0)
            return Normalize(file);

        var account = CreateAccount(
            "Default",
            fallbackKeyId,
            fallbackSecretKeyId,
            fallbackProjectId,
            fallbackEnvironmentId);

        file = new CloudSaveAdminAccountsFile
        {
            selectedId = account.id,
            accounts = new[] { account }
        };
        Save(file);
        return file;
    }

    public static void Save(string selectedId, IList<CloudSaveAdminAccount> accounts)
    {
        if (accounts == null || accounts.Count == 0)
        {
            Save(new CloudSaveAdminAccountsFile
            {
                selectedId = string.Empty,
                accounts = Array.Empty<CloudSaveAdminAccount>()
            });
            return;
        }

        var copy = new CloudSaveAdminAccount[accounts.Count];
        for (var i = 0; i < accounts.Count; i++)
            copy[i] = Clone(accounts[i]);

        Save(new CloudSaveAdminAccountsFile
        {
            selectedId = selectedId ?? string.Empty,
            accounts = copy
        });
    }

    public static CloudSaveAdminAccount CreateAccount(
        string title,
        string keyId = "",
        string secretKeyId = "",
        string projectId = "",
        string environmentId = "")
    {
        return new CloudSaveAdminAccount
        {
            id = Guid.NewGuid().ToString("N"),
            title = title?.Trim() ?? string.Empty,
            keyId = keyId?.Trim() ?? string.Empty,
            secretKeyId = secretKeyId ?? string.Empty,
            projectId = projectId?.Trim() ?? string.Empty,
            environmentId = environmentId?.Trim() ?? string.Empty,
            adjustToken = string.Empty,
            adjustS2sToken = string.Empty
        };
    }

    public static CloudSaveAdminAccount Clone(CloudSaveAdminAccount source)
    {
        if (source == null)
            return CreateAccount("New Account");

        return new CloudSaveAdminAccount
        {
            id = string.IsNullOrWhiteSpace(source.id) ? Guid.NewGuid().ToString("N") : source.id,
            title = source.title ?? string.Empty,
            keyId = source.keyId ?? string.Empty,
            secretKeyId = source.secretKeyId ?? string.Empty,
            projectId = source.projectId ?? string.Empty,
            environmentId = source.environmentId ?? string.Empty,
            adjustToken = source.adjustToken ?? string.Empty,
            adjustS2sToken = source.adjustS2sToken ?? string.Empty
        };
    }

    public static string UniqueTitle(IList<CloudSaveAdminAccount> accounts, string baseTitle, string exceptId = null)
    {
        baseTitle = string.IsNullOrWhiteSpace(baseTitle) ? "New Account" : baseTitle.Trim();
        if (!TitleExists(accounts, baseTitle, exceptId))
            return baseTitle;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseTitle} {i}";
            if (!TitleExists(accounts, candidate, exceptId))
                return candidate;
        }

        return $"{baseTitle} {Guid.NewGuid().ToString("N").Substring(0, 6)}";
    }

    static bool TitleExists(IList<CloudSaveAdminAccount> accounts, string title, string exceptId)
    {
        if (accounts == null)
            return false;

        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            if (account == null)
                continue;
            if (!string.IsNullOrEmpty(exceptId) && string.Equals(account.id, exceptId, StringComparison.Ordinal))
                continue;
            if (string.Equals((account.title ?? string.Empty).Trim(), title, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static CloudSaveAdminAccountsFile TryRead()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonUtility.FromJson<CloudSaveAdminAccountsFile>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read {FileName}: {ex.Message}");
            return null;
        }
    }

    static void Save(CloudSaveAdminAccountsFile file)
    {
        try
        {
            File.WriteAllText(GetFilePath(), JsonUtility.ToJson(file ?? new CloudSaveAdminAccountsFile(), true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to write {FileName}: {ex.Message}");
        }
    }

    static CloudSaveAdminAccountsFile Normalize(CloudSaveAdminAccountsFile file)
    {
        file ??= new CloudSaveAdminAccountsFile();
        var accounts = new List<CloudSaveAdminAccount>();
        if (file.accounts != null)
        {
            for (var i = 0; i < file.accounts.Length; i++)
            {
                var account = file.accounts[i];
                if (account == null)
                    continue;

                if (string.IsNullOrWhiteSpace(account.id))
                    account.id = Guid.NewGuid().ToString("N");
                account.title ??= string.Empty;
                account.keyId ??= string.Empty;
                account.secretKeyId ??= string.Empty;
                account.projectId ??= string.Empty;
                account.environmentId ??= string.Empty;
                account.adjustToken ??= string.Empty;
                account.adjustS2sToken ??= string.Empty;
                accounts.Add(account);
            }
        }

        if (accounts.Count == 0)
            accounts.Add(CreateAccount("Default"));

        file.accounts = accounts.ToArray();
        if (string.IsNullOrWhiteSpace(file.selectedId)
            || accounts.FindIndex(a => string.Equals(a.id, file.selectedId, StringComparison.Ordinal)) < 0)
            file.selectedId = accounts[0].id;

        return file;
    }
}
#endif
