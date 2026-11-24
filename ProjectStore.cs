using System.Text.Json;

namespace Kanzu;

public static class ProjectStore
{
    public static void Delete(string directory, string path)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        string target = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(target), root, comparison) ||
            !target.EndsWith(".kanzu.json", comparison))
        {
            throw new InvalidOperationException("Only saved projects inside the projects folder can be deleted.");
        }

        if (!File.Exists(target))
        {
            throw new FileNotFoundException("This project no longer exists.", target);
        }

        File.Delete(target);
    }

    public static List<SavedProject> Load(string directory, out int unreadableCount)
    {
        unreadableCount = 0;
        List<SavedProject> projects = [];
        if (!Directory.Exists(directory))
        {
            return projects;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.kanzu.json"))
        {
            try
            {
                BeatmapDraft draft = BeatmapDraft.Load(file);
                projects.Add(new SavedProject(file, draft, File.GetLastWriteTimeUtc(file)));
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                unreadableCount++;
            }
        }

        return projects.OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Path, StringComparer.Ordinal).ToList();
    }
}
