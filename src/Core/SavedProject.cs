namespace Kanzu;

public sealed record SavedProject(string Path, BeatmapDraft Draft, DateTime UpdatedAt);