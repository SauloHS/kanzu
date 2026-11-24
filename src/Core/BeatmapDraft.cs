using System.Text;
using System.Text.Json;

namespace Kanzu;

public sealed record BeatmapDraft
{
    public string Name { get; init; } = "";
    public string SongTitle { get; init; } = "";
    public string Artist { get; init; } = "";
    public int RequestedStars { get; init; } = 1;
    public string Tags { get; init; } = "";
    public string Description { get; init; } = "";
    public string BackgroundVideoUrl { get; init; } = "";
    public NewgroundsAudio Audio { get; init; } = new(0, "");
    public int FormatVersion { get; init; } = 2;

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return "Level name is required.";
        }

        if (string.IsNullOrWhiteSpace(SongTitle))
        {
            return "Song title is required.";
        }

        if (string.IsNullOrWhiteSpace(Artist))
        {
            return "Artist is required.";
        }

        if (RequestedStars is < 1 or > 10)
        {
            return "Stars Requested must be between 1 and 10.";
        }

        if (Audio == null || Audio.Id <= 0)
        {
            return "Enter a valid Newgrounds audio ID.";
        }

        if (!NewgroundsAudio.IsValidHash(Audio.Sha256))
        {
            return "Import the audio before saving.";
        }

        if (Tags == null || Description == null || BackgroundVideoUrl == null)
        {
            return "Optional text fields cannot be null.";
        }

        if (Name.Length > 48 || SongTitle.Length > 100 || Artist.Length > 100 ||
            Tags.Length > 160 || Description.Length > 500 || BackgroundVideoUrl.Length > 512)
        {
            return "A text field exceeds its character limit.";
        }

        if (!string.IsNullOrWhiteSpace(BackgroundVideoUrl) &&
            (!Uri.TryCreate(BackgroundVideoUrl, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return "Enter a complete http:// or https:// video URL.";
        }

        return null;
    }

    public string ToJson()
    {
        var metadata = new
        {
            name = Name,
            songTitle = SongTitle,
            artist = Artist,
            requestedStars = RequestedStars,
            tags = Tags,
            description = Description,
            backgroundVideoUrl = BackgroundVideoUrl,
            audio = new { id = Audio.Id, sha256 = Audio.Sha256 },
            formatVersion = FormatVersion
        };
        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
    }

    public static BeatmapDraft Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Project exceeds the 1 MB size limit.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        BeatmapDraft draft = JsonSerializer.Deserialize<BeatmapDraft>(reader.ReadToEnd(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The project is empty.");
        if (draft.FormatVersion is < 1 or > 2)
        {
            throw new InvalidDataException("This project uses an unsupported format version.");
        }

        string? error = draft.Validate();
        if (error != null)
        {
            throw new InvalidDataException(error);
        }

        return draft;
    }

    public string Save(string draftsDirectory, string? existingPath = null)
    {
        string? error = Validate();
        if (error != null)
        {
            throw new InvalidOperationException(error);
        }

        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(draftsDirectory));
        string target = existingPath == null
            ? Path.Combine(directory, $"{Guid.NewGuid():N}.kanzu.json")
            : Path.GetFullPath(existingPath);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(target), directory, comparison) ||
            !target.EndsWith(".kanzu.json", comparison))
        {
            throw new InvalidOperationException("Save path must be a project inside the drafts folder.");
        }

        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        bool created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(ToJson());
            }

            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
            File.Move(temporary, target, overwrite: existingPath != null);
            return target;
        }
        finally
        {
            if (created && File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
