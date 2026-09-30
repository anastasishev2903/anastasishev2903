#if UNITY_EDITOR
using System;
using System.Collections.Generic;

static class CloudSaveNetworkField
{
    public static void ApplyToRow(Dictionary<string, string> row)
    {
        if (row == null)
            return;

        row.TryGetValue("finalLink", out var finalLink);
        row["campaign"] = ReadQueryParam(finalLink, "sub_id_1");
        row["network"] = ReadQueryParam(finalLink, "ad_campaign_id");
    }

    static string ReadQueryParam(string url, string key)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrEmpty(key))
            return string.Empty;

        var queryStart = url.IndexOf('?');
        var query = queryStart >= 0 ? url.Substring(queryStart + 1) : url;
        var hash = query.IndexOf('#');
        if (hash >= 0)
            query = query.Substring(0, hash);

        var parts = query.Split('&');
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part))
                continue;

            var eq = part.IndexOf('=');
            var name = DecodeQueryComponent(eq >= 0 ? part.Substring(0, eq) : part);
            if (!string.Equals(name, key, StringComparison.Ordinal))
                continue;

            return DecodeQueryComponent(eq >= 0 ? part.Substring(eq + 1) : string.Empty);
        }

        return string.Empty;
    }

    static string DecodeQueryComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var text = value.Replace("+", " ");
        try
        {
            return Uri.UnescapeDataString(text);
        }
        catch (UriFormatException)
        {
            return text;
        }
    }
}
#endif
