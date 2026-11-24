using System.Diagnostics;
using System.Globalization;

namespace Kanzu;

public sealed class SongDownloadException(int code)
    : IOException($"Failed to download song: {code.ToString(CultureInfo.InvariantCulture)}")
{
    public int Code { get; } = code;
}

public static class NewgroundsDownloader
{
    public static async Task<ImportedAudio> DownloadAsync(long id, string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        string url = new NewgroundsAudio(id, "").DownloadUrl;
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.Combine(Path.GetTempPath(), "KanzuDownloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await DownloadFileAsync(url, directory, cancellationToken).ConfigureAwait(false);
            string downloadedPath = Path.Combine(directory, id.ToString(CultureInfo.InvariantCulture));
            string audioPath = downloadedPath + ".mp3";
            File.Move(downloadedPath, audioPath);
            cancellationToken.ThrowIfCancellationRequested();
            return await LocalAudio.ImportAsync(audioPath, cacheDirectory, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task DownloadFileAsync(string url, string directory, CancellationToken cancellationToken)
    {
        var options = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "curl.exe" : "curl",
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            "--disable", "-L", "-O", "--fail", "--silent", "--write-out", "%{http_code}",
            "--connect-timeout", "15", "--max-time", "120", "--max-filesize", "104857600",
            "--proto", "=https", "--proto-redir", "=https", url
        })
        {
            options.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = options };
        process.Start();
        Task<string> statusTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }

        string error = await errorTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            throw new IOException($"curl failed (exit {process.ExitCode}). {error.Trim()}");
        }
    }
}
