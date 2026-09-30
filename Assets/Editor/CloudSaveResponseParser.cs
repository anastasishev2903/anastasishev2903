#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

static class CloudSaveResponseParser
{
    public static Dictionary<string, string> ParsePublicItemsPage(string json, out string nextAfter)
    {
        nextAfter = null;
        var row = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(json))
            return row;

        var resultsStart = FindResultsArrayStart(json);
        if (resultsStart >= 0)
            ParseKeyValueObjects(json, resultsStart, row);

        nextAfter = ParseAfterFromPublicItemsResponse(json);
        return row;
    }

    public static string ParseAfterFromPublicItemsResponse(string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
            return null;

        var linksIndex = rawJson.IndexOf("\"links\"", StringComparison.Ordinal);
        if (linksIndex < 0)
            return null;

        var nextValue = ReadJsonStringField(rawJson, "next", linksIndex);
        return ParseAfterFromNextLink(nextValue);
    }

    public static string ParseAfterFromNextLink(string nextLink)
    {
        if (string.IsNullOrEmpty(nextLink))
            return null;

        foreach (var paramName in new[] { "start=", "after=" })
        {
            var paramIndex = nextLink.IndexOf(paramName, StringComparison.Ordinal);
            if (paramIndex < 0)
                continue;

            paramIndex += paramName.Length;
            var end = paramIndex;
            while (end < nextLink.Length && nextLink[end] != '&' && nextLink[end] != '"')
                end++;

            var cursor = Uri.UnescapeDataString(nextLink.Substring(paramIndex, end - paramIndex));
            if (!string.IsNullOrEmpty(cursor))
                return cursor;
        }

        if (!nextLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            && !nextLink.StartsWith("/", StringComparison.Ordinal)
            && LooksLikePaginationCursor(nextLink))
            return nextLink;

        return null;
    }

    static bool LooksLikePaginationCursor(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 255)
            return false;

        if (value.IndexOf('{') >= 0 || value.IndexOf('[') >= 0 || value.IndexOf('"') >= 0)
            return false;

        return true;
    }

    public static string ParseAfterFromPlayersListResponse(string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson))
            return null;

        var linksIndex = rawJson.IndexOf("\"links\"", StringComparison.Ordinal);
        if (linksIndex < 0)
            return null;

        var nextValue = ReadJsonStringField(rawJson, "next", linksIndex);
        return ParseAfterFromNextLink(nextValue);
    }

    public const int PlayersListApiPageSize = 20;
    public const int PlayerManagementListPageSize = 1000;
    public const int CloudSaveEntitiesListPageSize = 100;

    public static int CountResultsArray(string json)
    {
        var count = 0;
        if (string.IsNullOrEmpty(json))
            return count;

        var resultsStart = FindResultsArrayStart(json);
        if (resultsStart < 0)
            return count;

        var index = resultsStart;
        while (TryReadNextObject(json, ref index, out _))
            count++;

        return count;
    }

    public static string ReadNextPageToken(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var next = ReadJsonStringField(json, "next");
        return string.IsNullOrEmpty(next) ? null : next;
    }

    public static void ParsePlayersListPage(
        string rawJson,
        string currentAfter,
        out List<string> playerIds,
        out string nextAfter)
    {
        ParsePlayersListPage(rawJson, currentAfter, PlayersListApiPageSize, out playerIds, out nextAfter);
    }

    public static void ParsePlayersListPage(
        string rawJson,
        string currentAfter,
        int pageSize,
        out List<string> playerIds,
        out string nextAfter)
    {
        playerIds = new List<string>();
        if (string.IsNullOrEmpty(rawJson))
        {
            nextAfter = null;
            return;
        }

        var resultsStart = FindResultsArrayStart(rawJson);
        if (resultsStart >= 0)
        {
            var index = resultsStart;
            while (TryReadNextObject(rawJson, ref index, out var objectJson))
            {
                var playerId = ReadJsonStringField(objectJson, "id");
                if (!string.IsNullOrWhiteSpace(playerId))
                    playerIds.Add(playerId);
            }
        }

        nextAfter = ResolvePlayersListNextAfter(rawJson, playerIds, currentAfter, pageSize);
    }

    public static string ResolvePlayersListNextAfter(
        string rawJson,
        IReadOnlyList<string> playerIds,
        string currentAfter,
        int pageSize = PlayersListApiPageSize)
    {
        if (playerIds == null || playerIds.Count == 0)
            return null;

        var fromLink = ParseAfterFromPlayersListResponse(rawJson);
        if (!string.IsNullOrEmpty(fromLink) && fromLink != currentAfter)
            return fromLink;

        if (playerIds.Count < pageSize)
            return null;

        var lastPlayerId = playerIds[playerIds.Count - 1]?.Trim();
        if (string.IsNullOrEmpty(lastPlayerId) || lastPlayerId == currentAfter)
            return null;

        return lastPlayerId;
    }

    public static List<Dictionary<string, string>> ParseQueryResults(string json)
    {
        var rows = new List<Dictionary<string, string>>();
        if (string.IsNullOrEmpty(json))
            return rows;

        var resultsStart = FindResultsArrayStart(json);
        if (resultsStart < 0)
            return rows;

        var index = resultsStart;
        while (TryReadNextObject(json, ref index, out var objectJson))
        {
            var playerId = ReadJsonStringField(objectJson, "id");
            var row = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(playerId))
                row["playerId"] = playerId;

            var dataStart = objectJson.IndexOf("\"data\"", StringComparison.Ordinal);
            if (dataStart >= 0)
            {
                var arrayStart = objectJson.IndexOf('[', dataStart);
                if (arrayStart >= 0)
                    ParseKeyValueObjects(objectJson, arrayStart + 1, row);
            }

            rows.Add(row);
        }

        return rows;
    }

    static int FindResultsArrayStart(string json)
    {
        var marker = "\"results\"";
        var index = json.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            index += marker.Length;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ':')
            {
                index++;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == '[')
                    return index + 1;
            }

            index = json.IndexOf(marker, index, StringComparison.Ordinal);
        }

        return -1;
    }

    static void ParseKeyValueObjects(string json, int startIndex, Dictionary<string, string> row)
    {
        var index = startIndex;
        while (TryReadNextObject(json, ref index, out var objectJson))
        {
            var key = ReadJsonStringField(objectJson, "key");
            if (string.IsNullOrEmpty(key))
                continue;

            if (!TryReadJsonValueField(objectJson, "value", out var value))
                value = string.Empty;

            row[key] = value ?? string.Empty;
        }
    }

    static string ParseNextAfter(string json) => ParseAfterFromPlayersListResponse(json);

    public static string ReadJsonStringField(string json, string fieldName, int searchFrom = 0)
    {
        if (searchFrom > 0)
        {
            // Legacy path for scanning larger payloads (environments list, etc.).
            var fieldToken = "\"" + fieldName + "\"";
            var fieldIndex = json.IndexOf(fieldToken, searchFrom, StringComparison.Ordinal);
            if (fieldIndex < 0)
                return string.Empty;

            var index = fieldIndex + fieldToken.Length;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != ':')
                return string.Empty;

            index++;
            SkipWhitespace(json, ref index);
            if (!TryReadJsonString(json, index, out var legacyValue, out _))
                return string.Empty;

            return legacyValue;
        }

        if (!TryFindTopLevelObjectField(json, fieldName, out var valueStart))
            return string.Empty;

        if (!TryReadJsonString(json, valueStart, out var value, out _))
            return string.Empty;

        return value;
    }

    static bool TryReadJsonValueField(string json, string fieldName, out string value)
    {
        value = string.Empty;
        if (!TryFindTopLevelObjectField(json, fieldName, out var valueStart))
            return false;

        return TryReadJsonValue(json, valueStart, out value, out _);
    }

    /// <summary>
    /// Finds a top-level object field (depth 0, not inside a string/nested object) and
    /// returns the index of its value start. Prevents nested JSON like naming's own
    /// "value" keys from being mistaken for the Cloud Save item value.
    /// </summary>
    static bool TryFindTopLevelObjectField(string json, string fieldName, out int valueStart)
    {
        valueStart = -1;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            return false;

        var fieldToken = "\"" + fieldName + "\"";
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < json.Length; i++)
        {
            var ch = json[i];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (ch == '\\')
                    escaped = true;
                else if (ch == '"')
                    inString = false;
                continue;
            }

            if (ch == '"')
            {
                if (depth == 1
                    && i + fieldToken.Length <= json.Length
                    && string.Compare(json, i, fieldToken, 0, fieldToken.Length, StringComparison.Ordinal) == 0)
                {
                    var afterName = i + fieldToken.Length;
                    SkipWhitespace(json, ref afterName);
                    if (afterName < json.Length && json[afterName] == ':')
                    {
                        afterName++;
                        SkipWhitespace(json, ref afterName);
                        valueStart = afterName;
                        return afterName < json.Length;
                    }
                }

                inString = true;
                continue;
            }

            if (ch == '{' || ch == '[')
                depth++;
            else if (ch == '}' || ch == ']')
                depth = Math.Max(0, depth - 1);
        }

        return false;
    }

    static bool TryReadNextObject(string json, ref int index, out string objectJson)
    {
        objectJson = null;
        SkipWhitespace(json, ref index);
        if (index >= json.Length)
            return false;

        if (json[index] == ',')
        {
            index++;
            SkipWhitespace(json, ref index);
        }

        if (index >= json.Length || json[index] != '{')
            return false;

        var start = index;
        if (!TryReadBalanced(json, ref index, '{', '}'))
            return false;

        objectJson = json.Substring(start, index - start);
        return true;
    }

    static bool TryReadBalanced(string json, ref int index, char open, char close)
    {
        if (index >= json.Length || json[index] != open)
            return false;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (; index < json.Length; index++)
        {
            var ch = json[index];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (ch == '\\')
                    escaped = true;
                else if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == open)
                depth++;
            else if (ch == close)
            {
                depth--;
                if (depth == 0)
                {
                    index++;
                    return true;
                }
            }
        }

        return false;
    }

    static bool TryReadJsonValue(string json, int index, out string value, out int endIndex)
    {
        value = string.Empty;
        endIndex = index;
        if (index >= json.Length)
            return false;

        var ch = json[index];
        if (ch == '"')
            return TryReadJsonString(json, index, out value, out endIndex);

        if (ch == '{')
        {
            var cursor = index;
            if (!TryReadBalanced(json, ref cursor, '{', '}'))
                return false;

            value = json.Substring(index, cursor - index);
            endIndex = cursor;
            return true;
        }

        if (ch == '[')
        {
            var cursor = index;
            if (!TryReadBalanced(json, ref cursor, '[', ']'))
                return false;

            value = json.Substring(index, cursor - index);
            endIndex = cursor;
            return true;
        }

        if (index + 4 <= json.Length && json.Substring(index, 4) == "true")
        {
            value = "true";
            endIndex = index + 4;
            return true;
        }

        if (index + 5 <= json.Length && json.Substring(index, 5) == "false")
        {
            value = "false";
            endIndex = index + 5;
            return true;
        }

        if (index + 4 <= json.Length && json.Substring(index, 4) == "null")
        {
            value = string.Empty;
            endIndex = index + 4;
            return true;
        }

        var end = index;
        while (end < json.Length)
        {
            var current = json[end];
            if (current == ',' || current == '}' || current == ']')
                break;
            end++;
        }

        value = json.Substring(index, end - index).Trim();
        endIndex = end;
        return true;
    }

    static bool TryReadJsonString(string json, int index, out string value, out int endIndex)
    {
        value = string.Empty;
        endIndex = index;
        if (index >= json.Length || json[index] != '"')
            return false;

        var sb = new StringBuilder();
        var escaped = false;
        for (var i = index + 1; i < json.Length; i++)
        {
            var ch = json[i];
            if (escaped)
            {
                switch (ch)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(ch);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 < json.Length &&
                            int.TryParse(json.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                        {
                            sb.Append((char)codePoint);
                            i += 4;
                        }

                        break;
                    default:
                        sb.Append(ch);
                        break;
                }

                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                value = sb.ToString();
                endIndex = i + 1;
                return true;
            }

            sb.Append(ch);
        }

        return false;
    }

    static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
            index++;
    }
}
#endif
