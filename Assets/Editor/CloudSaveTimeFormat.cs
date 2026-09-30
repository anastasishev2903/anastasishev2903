#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;

static class CloudSaveTimeFormat
{
    public const string DisplayFormat = "dd.MM.yyyy HH:mm:ss";
    public const string DateDisplayFormat = "dd/MM/yyyy";
    public const string DateStorageFormat = "MM/dd/yyyy";

    public const string DateFieldKey = "date_utc_server";

    public static readonly string[] TimeFieldKeys = { "time_utc_user", "time_utc_server" };

    static readonly string[] DateDisplayParseFormats =
    {
        DateDisplayFormat,
        "d/M/yyyy",
        "dd/M/yyyy",
        "d/MM/yyyy",
        "dd.MM.yyyy",
        "d.M.yyyy"
    };

    static readonly string[] DateStorageParseFormats =
    {
        DateStorageFormat,
        "M/d/yyyy",
        "MM/d/yyyy",
        "M/dd/yyyy"
    };

    static readonly CultureInfo DisplayCulture = CultureInfo.InvariantCulture;
    static readonly CultureInfo StorageCulture = CultureInfo.GetCultureInfo("en-US");

    static readonly string[] ParseFormats =
    {
        DisplayFormat,
        "dd.MM.yyyy H:mm:ss",
        "dd.MM.yyyy HH:mm",
        "dd.MM.yyyy H:mm",
        "G",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ"
    };

    public static bool IsTimeField(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        for (var i = 0; i < TimeFieldKeys.Length; i++)
        {
            if (TimeFieldKeys[i] == key)
                return true;
        }

        return false;
    }

    public static bool IsDateField(string key) => key == DateFieldKey;

    public static string FormatFieldForDisplay(string key, string raw)
    {
        if (IsTimeField(key))
            return FormatForDisplay(raw);

        if (IsDateField(key))
            return FormatDateForDisplay(raw);

        return raw ?? string.Empty;
    }

    public static string FormatDateForDisplay(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw ?? string.Empty;

        return TryParseDateValue(raw.Trim(), out var date)
            ? date.ToString(DateDisplayFormat, DisplayCulture)
            : raw;
    }

    public static bool TryFormatDateForCloudSaveQuery(string input, out string queryValue)
    {
        queryValue = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!TryParseDateInput(input.Trim(), out var date))
            return false;

        queryValue = FormatStoredDate(date);
        return true;
    }

    public static int CompareField(string key, string left, string right)
    {
        if (IsTimeField(key))
            return Compare(left, right);

        if (IsDateField(key))
            return CompareDate(left, right);

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static int CompareDate(string left, string right)
    {
        var leftParsed = TryParseDateValue(left, out var leftDate);
        var rightParsed = TryParseDateValue(right, out var rightDate);

        if (leftParsed && rightParsed)
            return leftDate.CompareTo(rightDate);

        if (leftParsed != rightParsed)
            return leftParsed ? -1 : 1;

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatForDisplay(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw ?? string.Empty;

        return TryParse(raw, out var utc)
            ? FormatDisplay(utc)
            : raw;
    }

    public static string FormatDisplay(DateTime utc) =>
        utc.ToString(DisplayFormat, DisplayCulture);

    public static string FormatForCloudSaveQuery(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (!TryParse(input.Trim(), out var utc))
            return null;

        return FormatStorage(utc);
    }

    public static bool TryFormatForCloudSaveQuery(string input, out string queryValue)
    {
        queryValue = FormatForCloudSaveQuery(input);
        return queryValue != null;
    }

    public static void NormalizeRow(Dictionary<string, string> row)
    {
        if (row == null)
            return;

        foreach (var key in TimeFieldKeys)
        {
            if (!row.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            row[key] = FormatForDisplay(raw);
        }

        if (row.TryGetValue(DateFieldKey, out var dateRaw) && !string.IsNullOrWhiteSpace(dateRaw))
            row[DateFieldKey] = FormatDateForDisplay(dateRaw);

        CloudSaveNetworkField.ApplyToRow(row);
    }

    public static bool TryParse(string input, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim();
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (DateTime.TryParseExact(text, ParseFormats, DisplayCulture, styles, out utc))
            return true;

        if (DateTime.TryParseExact(text, ParseFormats, StorageCulture, styles, out utc))
            return true;

        if (DateTime.TryParse(text, StorageCulture, styles, out utc))
            return true;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out utc))
            return true;

        return DateTime.TryParse(text, CultureInfo.CurrentCulture, styles, out utc);
    }

    public static int Compare(string left, string right)
    {
        var leftParsed = TryParse(left, out var leftUtc);
        var rightParsed = TryParse(right, out var rightUtc);

        if (leftParsed && rightParsed)
            return leftUtc.CompareTo(rightUtc);

        if (leftParsed != rightParsed)
            return leftParsed ? -1 : 1;

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static string FormatStorage(DateTime utc) =>
        utc.ToString(CultureInfo.InvariantCulture);

    static string FormatStoredDate(DateTime date) =>
        date.ToString(DateStorageFormat, StorageCulture);

    static bool TryParseDateInput(string input, out DateTime date)
    {
        date = default;
        if (DateTime.TryParseExact(input, DateDisplayParseFormats, DisplayCulture, DateTimeStyles.None, out date))
        {
            date = date.Date;
            return true;
        }

        return false;
    }

    static bool TryParseDateValue(string input, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim();
        return TryParseUsStorageDate(text, out date)
            || TryParseDateInput(text, out date)
            || TryParseStoredDate(text, out date);
    }

    static bool TryParseUsStorageDate(string input, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim();
        var firstSlash = text.IndexOf('/');
        if (firstSlash <= 0)
            return false;

        var secondSlash = text.IndexOf('/', firstSlash + 1);
        if (secondSlash <= firstSlash + 1)
            return false;

        if (!int.TryParse(text.Substring(0, firstSlash), out var month))
            return false;

        if (!int.TryParse(text.Substring(firstSlash + 1, secondSlash - firstSlash - 1), out var day))
            return false;

        var yearPart = text.Substring(secondSlash + 1);
        var spaceIndex = yearPart.IndexOf(' ');
        if (spaceIndex >= 0)
            yearPart = yearPart.Substring(0, spaceIndex);

        if (!int.TryParse(yearPart, out var year))
            return false;

        if (month < 1 || month > 12 || day < 1 || day > 31)
            return false;

        try
        {
            date = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    static bool TryParseStoredDate(string input, out DateTime date)
    {
        date = default;
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces;

        if (DateTime.TryParseExact(input, DateStorageParseFormats, StorageCulture, styles, out date))
        {
            date = date.Date;
            return true;
        }

        if (DateTime.TryParseExact(input, DateStorageParseFormats, CultureInfo.InvariantCulture, styles, out date))
        {
            date = date.Date;
            return true;
        }

        if (DateTime.TryParse(input, StorageCulture, styles, out date))
        {
            date = date.Date;
            return true;
        }

        return false;
    }
}
#endif
