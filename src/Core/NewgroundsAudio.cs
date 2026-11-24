using System.Globalization;
using System.Text.Json.Serialization;

namespace Kanzu;

public sealed record NewgroundsAudio(long Id, string Sha256)
{
    [JsonIgnore]
    public string DownloadUrl
    {
        get
        {
            if (Id <= 0)
            {
                throw new ArgumentException("Invalid Newgrounds audio ID: must be positive", nameof(Id));
            }

            return $"https://www.newgrounds.com/audio/download/{Id.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    public static string CleanTitle(string title, long id)
    {
        title = title.Trim();
        string suffix = $"(ID: {id.ToString(CultureInfo.InvariantCulture)})";
        return title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? title[..^suffix.Length].TrimEnd()
            : title;
    }

    [JsonIgnore]
    public string PageUrl
    {
        get
        {
            if (Id <= 0)
            {
                throw new ArgumentException("Invalid Newgrounds audio ID: must be positive", nameof(Id));
            }
            return $"https://www.newgrounds.com/audio/listen/{Id}";
        }
    }

    public static bool TryParseId(string value, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        foreach (var c in trimmed)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return long.TryParse(trimmed, out id) && id > 0;
    }

    public static bool IsValidHash(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesHash(string expected, string actual)
    {
        if (!IsValidHash(expected) || !IsValidHash(actual))
        {
            return false;
        }

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}