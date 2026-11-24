namespace Kanzu;

using System.IO;
using System.Security.Cryptography;
using System.Text;

public sealed record ImportedAudio(
    string FileName,
    string Sha256,
    string LocalPath,
    long SizeBytes,
    bool CreatedCacheFile
);

public static class LocalAudio
{
    private static readonly SemaphoreSlim CacheWrite = new(1, 1);
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".ogg", ".wav"
    };

    public static bool IsSupportedFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    public static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Audio file not found", path);
        }

        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<ImportedAudio> ImportAsync(
        string sourcePath,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupportedFile(sourcePath))
        {
            throw new ArgumentException($"Unsupported audio format: {Path.GetExtension(sourcePath)}", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source audio file not found", sourcePath);
        }

        var sourceInfo = new FileInfo(sourcePath);
        if (sourceInfo.Length == 0)
        {
            throw new ArgumentException("Source audio file is empty", nameof(sourcePath));
        }

        Directory.CreateDirectory(cacheDirectory);

        string hash;
        long copiedBytes = 0;

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var tempPath = Path.Combine(cacheDirectory, $".{Guid.NewGuid():N}.tmp");

        try
        {
            using (var sourceStream = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var tempStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var sha256 = SHA256.Create())
            {
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await tempStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    copiedBytes += bytesRead;
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            }

            if (copiedBytes == 0)
            {
                throw new ArgumentException("Source audio file is empty", nameof(sourcePath));
            }

            var finalFileName = hash + extension;
            var finalPath = Path.Combine(cacheDirectory, finalFileName);

            await CacheWrite.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(finalPath))
                {
                    string existingHash = await ComputeHashAsync(finalPath, cancellationToken);
                    if (string.Equals(existingHash, hash, StringComparison.Ordinal))
                    {
                        File.Delete(tempPath);
                        return new ImportedAudio(sourceInfo.Name, hash, finalPath, copiedBytes, false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(tempPath, finalPath, true);
                return new ImportedAudio(sourceInfo.Name, hash, finalPath, copiedBytes, true);
            }
            finally
            {
                CacheWrite.Release();
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }
}