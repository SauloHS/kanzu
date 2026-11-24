using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kanzu;

if (args.Length == 2 && args[0] == "--native")
{
    NativeEditorSmoke.Run(args[1]);
    return;
}

if (args.Length == 3 && args[0] == "--download")
{
    long songId = long.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    try
    {
        ImportedAudio audio = await NewgroundsDownloader.DownloadAsync(songId, args[2]);
        Console.WriteLine($"Downloaded: {audio.FileName}");
        Console.WriteLine($"Title: {NewgroundsAudio.CleanTitle(audio.Title, songId)}");
        Console.WriteLine($"Artist: {audio.Artist}");
        Console.WriteLine($"SHA-256: {audio.Sha256}");
    }
    catch (SongDownloadException error)
    {
        Console.WriteLine(error.Message);
        Environment.ExitCode = 1;
    }

    return;
}

var passCount = 0;
var failCount = 0;

void Assert(string name, bool condition, string message = "")
{
    if (condition)
    {
        Console.WriteLine($"  PASS: {name}");
        passCount++;
    }
    else
    {
        Console.WriteLine($"  FAIL: {name} - {message}");
        failCount++;
    }
}

void AssertEqual<T>(string name, T expected, T actual)
{
    if (EqualityComparer<T>.Default.Equals(expected, actual))
    {
        Console.WriteLine($"  PASS: {name}");
        passCount++;
    }
    else
    {
        Console.WriteLine($"  FAIL: {name} - expected '{expected}', got '{actual}'");
        failCount++;
    }
}

void AssertThrows<T>(string name, Action action) where T : Exception
{
    try
    {
        action();
        Console.WriteLine($"  FAIL: {name} - expected {typeof(T).Name} but no exception thrown");
        failCount++;
    }
    catch (T)
    {
        Console.WriteLine($"  PASS: {name}");
        passCount++;
    }
    catch (AggregateException aex) when (aex.InnerExceptions.Any(e => e is T))
    {
        Console.WriteLine($"  PASS: {name}");
        passCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL: {name} - expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}");
        failCount++;
    }
}

Console.WriteLine("=== NewgroundsAudio Tests ===");

Console.WriteLine("TryParseId:");
Assert("valid positive id", NewgroundsAudio.TryParseId("123", out var id1) && id1 == 123);
Assert("valid positive id with whitespace", NewgroundsAudio.TryParseId("  456  ", out var id2) && id2 == 456);
Assert("zero id fails", !NewgroundsAudio.TryParseId("0", out _));
Assert("negative id fails", !NewgroundsAudio.TryParseId("-1", out _));
Assert("non-numeric fails", !NewgroundsAudio.TryParseId("abc", out _));
Assert("empty string fails", !NewgroundsAudio.TryParseId("", out _));
Assert("whitespace only fails", !NewgroundsAudio.TryParseId("   ", out _));
Assert("decimal fails", !NewgroundsAudio.TryParseId("123.45", out _));
Assert("max long value", NewgroundsAudio.TryParseId(long.MaxValue.ToString(), out var id3) && id3 == long.MaxValue);
Assert("overflow fails", !NewgroundsAudio.TryParseId((long.MaxValue + 1UL).ToString(), out _));
Assert("plus sign rejected", !NewgroundsAudio.TryParseId("+123", out _));
Assert("minus sign rejected", !NewgroundsAudio.TryParseId("-123", out _));
Assert("scientific notation rejected", !NewgroundsAudio.TryParseId("1e3", out _));
Assert("float rejected", !NewgroundsAudio.TryParseId("1.0", out _));
Assert("leading zeros allowed", NewgroundsAudio.TryParseId("00123", out var id4) && id4 == 123);
Assert("non-ascii digits rejected", !NewgroundsAudio.TryParseId("１２３", out _));
Assert("arabic digits rejected", !NewgroundsAudio.TryParseId("١٢٣", out _));

Console.WriteLine("IsValidHash:");
Assert("valid 64-char lowercase hex", NewgroundsAudio.IsValidHash(new string('a', 64)));
Assert("valid 64-char uppercase hex", NewgroundsAudio.IsValidHash(new string('F', 64)));
Assert("valid 64-char mixed case hex", NewgroundsAudio.IsValidHash("abcdef0123456789ABCDEF0123456789abcdef0123456789ABCDEF0123456789"));
Assert("63 chars fails", !NewgroundsAudio.IsValidHash(new string('a', 63)));
Assert("65 chars fails", !NewgroundsAudio.IsValidHash(new string('a', 65)));
Assert("empty string fails", !NewgroundsAudio.IsValidHash(""));
Assert("null fails", !NewgroundsAudio.IsValidHash(null!));
Assert("non-hex char fails", !NewgroundsAudio.IsValidHash(new string('a', 63) + "g"));
Assert("space char fails", !NewgroundsAudio.IsValidHash(new string('a', 63) + " "));

Console.WriteLine("MatchesHash:");
var validHash1 = new string('a', 64);
var validHash2 = new string('b', 64);
var validHashUpper = new string('A', 64);
Assert("matching lowercase", NewgroundsAudio.MatchesHash(validHash1, validHash1));
Assert("matching case insensitive", NewgroundsAudio.MatchesHash(validHashUpper, validHash1));
Assert("mismatch fails", !NewgroundsAudio.MatchesHash(validHash1, validHash2));
Assert("invalid expected fails", !NewgroundsAudio.MatchesHash("ggg", validHash1));
Assert("invalid actual fails", !NewgroundsAudio.MatchesHash(validHash1, "ggg"));
Assert("both invalid fails", !NewgroundsAudio.MatchesHash("ggg", "hhh"));

Console.WriteLine("PageUrl:");
var audio1 = new NewgroundsAudio(123, new string('a', 64));
AssertEqual("valid positive id", "https://www.newgrounds.com/audio/listen/123", audio1.PageUrl);
AssertThrows<ArgumentException>("zero id throws", () => { var _ = new NewgroundsAudio(0, new string('a', 64)).PageUrl; });
AssertThrows<ArgumentException>("negative id throws", () => { var _ = new NewgroundsAudio(-1, new string('a', 64)).PageUrl; });

Console.WriteLine("DownloadUrl:");
AssertEqual("download uses official endpoint", "https://www.newgrounds.com/audio/download/123", audio1.DownloadUrl);
AssertThrows<ArgumentException>("zero download id throws", () => { var _ = new NewgroundsAudio(0, "").DownloadUrl; });
AssertThrows<ArgumentException>("negative download id throws", () => { var _ = new NewgroundsAudio(-1, "").DownloadUrl; });
Assert("download URL is not serialized", !JsonSerializer.Serialize(audio1).Contains("DownloadUrl"));
AssertThrows<ArgumentException>("invalid id cannot start download",
    () => NewgroundsDownloader.DownloadAsync(0, Path.GetTempPath()).GetAwaiter().GetResult());
using (var canceledDownload = new CancellationTokenSource())
{
    canceledDownload.Cancel();
    AssertThrows<OperationCanceledException>("canceled download does not start curl",
        () => NewgroundsDownloader.DownloadAsync(123, Path.GetTempPath(), canceledDownload.Token).GetAwaiter().GetResult());
}

Console.WriteLine("CleanTitle:");
AssertEqual("Newgrounds ID suffix removed", "Test Song", NewgroundsAudio.CleanTitle("Test Song (ID: 123)", 123));
AssertEqual("title whitespace trimmed", "Test Song", NewgroundsAudio.CleanTitle("  Test Song  (ID: 123)  ", 123));
AssertEqual("suffix matching is case insensitive", "Test Song", NewgroundsAudio.CleanTitle("Test Song (id: 123)", 123));
AssertEqual("plain title unchanged", "Test Song", NewgroundsAudio.CleanTitle("Test Song", 123));
AssertEqual("different ID suffix preserved", "Test Song (ID: 456)", NewgroundsAudio.CleanTitle("Test Song (ID: 456)", 123));
AssertEqual("ID inside title preserved", "Test (ID: 123) Remix", NewgroundsAudio.CleanTitle("Test (ID: 123) Remix", 123));
AssertEqual("empty title supported", "", NewgroundsAudio.CleanTitle("", 123));

Console.WriteLine("\n=== LocalAudio Tests ===");

var testRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var cacheDir = Path.Combine(testRoot, "cache");
var sourceDir = Path.Combine(testRoot, "source");
Directory.CreateDirectory(cacheDir);
Directory.CreateDirectory(sourceDir);

try
{
    Console.WriteLine("IsSupportedFile:");
    Assert("mp3 supported", LocalAudio.IsSupportedFile("test.mp3"));
    Assert("MP3 supported", LocalAudio.IsSupportedFile("test.MP3"));
    Assert("ogg supported", LocalAudio.IsSupportedFile("test.ogg"));
    Assert("wav supported", LocalAudio.IsSupportedFile("test.wav"));
    Assert("WAV supported", LocalAudio.IsSupportedFile("test.WAV"));
    Assert("flac not supported", !LocalAudio.IsSupportedFile("test.flac"));
    Assert("txt not supported", !LocalAudio.IsSupportedFile("test.txt"));
    Assert("no extension not supported", !LocalAudio.IsSupportedFile("test"));
    Assert("null not supported", !LocalAudio.IsSupportedFile(null!));
    Assert("empty not supported", !LocalAudio.IsSupportedFile(""));

    Console.WriteLine("ComputeHashAsync:");
    var testFile1 = Path.Combine(sourceDir, "test1.mp3");
    var testBytes1 = Encoding.UTF8.GetBytes("test audio content 1");
    await File.WriteAllBytesAsync(testFile1, testBytes1);
    var hash1 = await LocalAudio.ComputeHashAsync(testFile1);
    var expectedHash1 = ComputeSha256Hex(testBytes1);
    AssertEqual("hash matches independent calculation", expectedHash1, hash1);
    Assert("hash is lowercase", hash1 == hash1.ToLowerInvariant());

    var testFile2 = Path.Combine(sourceDir, "test2.mp3");
    var testBytes2 = Encoding.UTF8.GetBytes("test audio content 2");
    await File.WriteAllBytesAsync(testFile2, testBytes2);
    var hash2 = await LocalAudio.ComputeHashAsync(testFile2);
    Assert("different content different hash", hash1 != hash2);

    Console.WriteLine("ImportAsync - basic import:");
    var imported1 = await LocalAudio.ImportAsync(testFile1, cacheDir);
    AssertEqual("filename preserved", "test1.mp3", imported1.FileName);
    AssertEqual("hash matches", expectedHash1, imported1.Sha256);
    Assert("local path exists", File.Exists(imported1.LocalPath));
    AssertEqual("size matches", testBytes1.LongLength, imported1.SizeBytes);
    Assert("created cache file", imported1.CreatedCacheFile);
    Assert("cached file name is hash+ext", Path.GetFileName(imported1.LocalPath) == expectedHash1 + ".mp3");

    Console.WriteLine("ImportAsync - deduplication:");
    var imported1Again = await LocalAudio.ImportAsync(testFile1, cacheDir);
    AssertEqual("same hash returned", imported1.Sha256, imported1Again.Sha256);
    AssertEqual("same path returned", imported1.LocalPath, imported1Again.LocalPath);
    Assert("created cache file false on reuse", !imported1Again.CreatedCacheFile);

    Console.WriteLine("ImportAsync - different file same cache dir:");
    var imported2 = await LocalAudio.ImportAsync(testFile2, cacheDir);
    Assert("different hash", imported1.Sha256 != imported2.Sha256);
    Assert("different path", imported1.LocalPath != imported2.LocalPath);
    Assert("created cache file true", imported2.CreatedCacheFile);

    Console.WriteLine("ImportAsync - tampered cache file replaced:");
    var tamperedPath = imported1.LocalPath;
    await File.WriteAllBytesAsync(tamperedPath, Encoding.UTF8.GetBytes("corrupted"));
    var imported1Repaired = await LocalAudio.ImportAsync(testFile1, cacheDir);
    AssertEqual("hash still correct", expectedHash1, imported1Repaired.Sha256);
    Assert("created cache file true after repair", imported1Repaired.CreatedCacheFile);
    var repairedBytes = await File.ReadAllBytesAsync(imported1Repaired.LocalPath);
    AssertEqual("content restored", testBytes1.Length, repairedBytes.Length);

    Console.WriteLine("ImportAsync - empty file rejected:");
    var emptyFile = Path.Combine(sourceDir, "empty.mp3");
    await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());
    AssertThrows<ArgumentException>("empty file throws", () => LocalAudio.ImportAsync(emptyFile, cacheDir).Wait());

    Console.WriteLine("ImportAsync - missing file rejected:");
    AssertThrows<FileNotFoundException>("missing file throws", () => LocalAudio.ImportAsync(Path.Combine(sourceDir, "missing.mp3"), cacheDir).Wait());

    Console.WriteLine("ImportAsync - unsupported extension rejected:");
    var txtFile = Path.Combine(sourceDir, "test.txt");
    await File.WriteAllTextAsync(txtFile, "not audio");
    AssertThrows<ArgumentException>("unsupported extension throws", () => LocalAudio.ImportAsync(txtFile, cacheDir).Wait());

    Console.WriteLine("ImportAsync - cancellation cleanup:");
    var largeFile = Path.Combine(sourceDir, "large.mp3");
    var largeBytes = new byte[1024 * 1024];
    new Random(42).NextBytes(largeBytes);
    await File.WriteAllBytesAsync(largeFile, largeBytes);
    var cts = new CancellationTokenSource();
    var importTask = LocalAudio.ImportAsync(largeFile, cacheDir, cts.Token);
    cts.Cancel();
    try
    {
        await importTask;
        Console.WriteLine($"  FAIL: ImportAsync - cancellation should have thrown");
        failCount++;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine($"  PASS: ImportAsync - cancellation throws OperationCanceledException");
        passCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL: ImportAsync - cancellation threw {ex.GetType().Name}: {ex.Message}");
        failCount++;
    }
    var tempFiles = Directory.GetFiles(cacheDir, ".*.tmp");
    Assert("no temp files left after cancellation", tempFiles.Length == 0);

    Console.WriteLine("ImportAsync - concurrent imports don't overwrite:");
    var concurrentFile = Path.Combine(sourceDir, "concurrent.mp3");
    var concurrentBytes = Encoding.UTF8.GetBytes("concurrent test");
    await File.WriteAllBytesAsync(concurrentFile, concurrentBytes);
    var concurrentHash = ComputeSha256Hex(concurrentBytes);
    var tasks = new List<Task<ImportedAudio>>();
    for (int i = 0; i < 5; i++)
    {
        tasks.Add(LocalAudio.ImportAsync(concurrentFile, cacheDir));
    }
    var results = await Task.WhenAll(tasks);
    foreach (var r in results)
    {
        AssertEqual("concurrent hash correct", concurrentHash, r.Sha256);
        Assert("concurrent file exists", File.Exists(r.LocalPath));
    }
    var cacheFiles = Directory.GetFiles(cacheDir, concurrentHash + ".mp3");
    Assert("only one cache file exists", cacheFiles.Length == 1);

    Console.WriteLine("ImportAsync - original file unmodified:");
    var originalBytes = await File.ReadAllBytesAsync(testFile1);
    AssertEqual("original bytes unchanged", testBytes1.Length, originalBytes.Length);
    Assert("original content unchanged", testBytes1.SequenceEqual(originalBytes));

    Console.WriteLine("ImportAsync - source disappears during import:");
    var disappearingFile = Path.Combine(sourceDir, "disappearing.mp3");
    var disappearingBytes = Encoding.UTF8.GetBytes("disappearing content");
    await File.WriteAllBytesAsync(disappearingFile, disappearingBytes);
    var disappearingHash = ComputeSha256Hex(disappearingBytes);
    File.Delete(disappearingFile);
    AssertThrows<FileNotFoundException>("source gone throws", () => LocalAudio.ImportAsync(disappearingFile, cacheDir).GetAwaiter().GetResult());

    Console.WriteLine("ImportAsync - cache disappears during import:");
    var cacheTestFile = Path.Combine(sourceDir, "cachetest.mp3");
    var cacheTestBytes = Encoding.UTF8.GetBytes("cache test content");
    await File.WriteAllBytesAsync(cacheTestFile, cacheTestBytes);
    var cacheTestHash = ComputeSha256Hex(cacheTestBytes);
    var cacheTestPath = Path.Combine(cacheDir, cacheTestHash + ".mp3");
    await File.WriteAllBytesAsync(cacheTestPath, cacheTestBytes);
    File.Delete(cacheTestPath);
    var importedCacheTest = await LocalAudio.ImportAsync(cacheTestFile, cacheDir);
    AssertEqual("recreated cache hash correct", cacheTestHash, importedCacheTest.Sha256);
    Assert("recreated cache file exists", File.Exists(importedCacheTest.LocalPath));
    Assert("created cache file true after cache disappeared", importedCacheTest.CreatedCacheFile);

    Console.WriteLine("ImportAsync - cache mismatch with source (changed source):");
    var mismatchFile = Path.Combine(sourceDir, "mismatch.mp3");
    var mismatchBytes1 = Encoding.UTF8.GetBytes("mismatch content v1");
    var mismatchBytes2 = Encoding.UTF8.GetBytes("mismatch content v2");
    await File.WriteAllBytesAsync(mismatchFile, mismatchBytes1);
    var mismatchHash1 = ComputeSha256Hex(mismatchBytes1);
    var importedMismatch1 = await LocalAudio.ImportAsync(mismatchFile, cacheDir);
    AssertEqual("first import hash", mismatchHash1, importedMismatch1.Sha256);
    await File.WriteAllBytesAsync(mismatchFile, mismatchBytes2);
    var importedMismatch2 = await LocalAudio.ImportAsync(mismatchFile, cacheDir);
    var mismatchHash2 = ComputeSha256Hex(mismatchBytes2);
    AssertEqual("second import hash matches new content", mismatchHash2, importedMismatch2.Sha256);
    Assert("created cache file true on content change", importedMismatch2.CreatedCacheFile);

    Console.WriteLine("ImportAsync - embedded metadata:");
    string taggedPath = Path.Combine(sourceDir, "tagged.mp3");
    File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "main-menu", "music.mp3"), taggedPath);
    using (var taggedFile = TagLib.File.Create(taggedPath))
    {
        taggedFile.Tag.Title = "Café Song (ID: 123)";
        taggedFile.Tag.Performers = ["Test Artist"];
        taggedFile.Save();
    }

    string taggedHash = await LocalAudio.ComputeHashAsync(taggedPath);
    ImportedAudio taggedAudio = await LocalAudio.ImportAsync(taggedPath, cacheDir);
    AssertEqual("embedded MP3 title read", "Café Song (ID: 123)", taggedAudio.Title);
    AssertEqual("embedded MP3 artist read", "Test Artist", taggedAudio.Artist);
    AssertEqual("embedded title cleaned", "Café Song", NewgroundsAudio.CleanTitle(taggedAudio.Title, 123));
    AssertEqual("metadata reading preserves original hash", taggedHash, taggedAudio.Sha256);
    AssertEqual("metadata reading leaves source unchanged", taggedHash, await LocalAudio.ComputeHashAsync(taggedPath));
    AssertEqual("metadata reading leaves cache unchanged", taggedHash, await LocalAudio.ComputeHashAsync(taggedAudio.LocalPath));
    ImportedAudio reusedAudio = await LocalAudio.ImportAsync(taggedPath, cacheDir);
    AssertEqual("reused cache keeps title", taggedAudio.Title, reusedAudio.Title);
    AssertEqual("reused cache keeps artist", taggedAudio.Artist, reusedAudio.Artist);
    Assert("reused tagged audio does not duplicate cache", !reusedAudio.CreatedCacheFile);
    AssertEqual("invalid audio has no title", "", importedMismatch2.Title);
    AssertEqual("invalid audio has no artist", "", importedMismatch2.Artist);
}
finally
{
    try { Directory.Delete(testRoot, true); } catch { }
}

Console.WriteLine("\n=== BeatmapDraft Tests ===");

Console.WriteLine("Validate:");
var validDraft = new BeatmapDraft
{
    Name = "Test Map",
    SongTitle = "Test Song",
    Artist = "Test Artist",
    RequestedStars = 5,
    Audio = new NewgroundsAudio(123, new string('a', 64))
};
Assert("valid draft passes", validDraft.Validate() == null);

Assert("empty name fails", new BeatmapDraft { Name = "", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("whitespace name fails", new BeatmapDraft { Name = "  ", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("empty song title fails", new BeatmapDraft { Name = "x", SongTitle = "", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("empty artist fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("requestedStars 0 fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 0, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("requestedStars 11 fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 11, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("requestedStars 1 passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() == null);
Assert("requestedStars 10 passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 10, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() == null);
Assert("zero audio id fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(0, new string('a', 64)) }.Validate() != null);
Assert("negative audio id fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(-1, new string('a', 64)) }.Validate() != null);
Assert("invalid audio hash fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, "invalid") }.Validate() != null);
Assert("short audio hash fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 63)) }.Validate() != null);
Assert("null audio fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = null! }.Validate() != null);

Assert("valid http background video url passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "http://example.com/video.mp4" }.Validate() == null);
Assert("valid https background video url passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "https://example.com/video.mp4" }.Validate() == null);
Assert("relative background video url fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "/video.mp4" }.Validate() != null);
Assert("ftp background video url fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "ftp://example.com/video.mp4" }.Validate() != null);
Assert("empty background video url passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "" }.Validate() == null);

Assert("name 48 chars passes", new BeatmapDraft { Name = new string('x', 48), SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() == null);
Assert("name 49 chars fails", new BeatmapDraft { Name = new string('x', 49), SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("songTitle 100 chars passes", new BeatmapDraft { Name = "x", SongTitle = new string('x', 100), Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() == null);
Assert("songTitle 101 chars fails", new BeatmapDraft { Name = "x", SongTitle = new string('x', 101), Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("artist 100 chars passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = new string('x', 100), RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() == null);
Assert("artist 101 chars fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = new string('x', 101), RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) }.Validate() != null);
Assert("tags 160 chars passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), Tags = new string('x', 160) }.Validate() == null);
Assert("tags 161 chars fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), Tags = new string('x', 161) }.Validate() != null);
Assert("description 500 chars passes", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), Description = new string('x', 500) }.Validate() == null);
Assert("description 501 chars fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), Description = new string('x', 501) }.Validate() != null);
var url512 = "https://example.com/" + new string('x', 492);
var draft512 = new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = url512 };
var error512 = draft512.Validate();
Assert("backgroundVideoUrl 512 chars passes", error512 == null, error512 ?? "null");
Assert("backgroundVideoUrl 513 chars fails", new BeatmapDraft { Name = "x", SongTitle = "x", Artist = "x", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)), BackgroundVideoUrl = "https://example.com/" + new string('x', 493) }.Validate() != null);

Console.WriteLine("ToJson:");
var jsonDraft = new BeatmapDraft
{
    Name = "Test Map",
    SongTitle = "Test Song",
    Artist = "Test Artist",
    RequestedStars = 7,
    Tags = "tag1,tag2",
    Description = "A test map",
    BackgroundVideoUrl = "https://example.com/video.mp4",
    Audio = new NewgroundsAudio(123, "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
    FormatVersion = 2
};
var json = jsonDraft.ToJson();
Assert("json contains name", json.Contains("\"name\": \"Test Map\""));
Assert("json contains songTitle", json.Contains("\"songTitle\": \"Test Song\""));
Assert("json contains artist", json.Contains("\"artist\": \"Test Artist\""));
Assert("json contains requestedStars as integer", json.Contains("\"requestedStars\": 7"));
Assert("json does not contain difficulty", !json.Contains("\"difficulty\""));
Assert("json contains tags", json.Contains("\"tags\": \"tag1,tag2\""));
Assert("json contains description", json.Contains("\"description\": \"A test map\""));
Assert("json contains backgroundVideoUrl", json.Contains("\"backgroundVideoUrl\": \"https://example.com/video.mp4\""));
Assert("json contains audio id", json.Contains("\"id\": 123"));
Assert("json contains audio sha256", json.Contains("\"sha256\": \"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789\""));
Assert("json contains formatVersion", json.Contains("\"formatVersion\": 2"));
Assert("json does not contain local path", !json.Contains("LocalPath"));
Assert("json does not contain page url", !json.Contains("pageUrl"));
Assert("json does not contain source filename", !json.Contains("test1.mp3"));
Assert("json does not contain created cache file", !json.Contains("createdCacheFile"));
Assert("json does not contain size bytes", !json.Contains("sizeBytes"));

var parsed = JsonDocument.Parse(json);
Assert("json is valid", parsed.RootElement.ValueKind == JsonValueKind.Object);
Assert("requestedStars is number not string", parsed.RootElement.TryGetProperty("requestedStars", out var rsProp) && rsProp.ValueKind == JsonValueKind.Number);

Console.WriteLine("Save:");
var saveRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var draftsDir = Path.Combine(saveRoot, "drafts");
try
{
    var savedPath = jsonDraft.Save(draftsDir);
    Assert("save returns absolute path", Path.IsPathRooted(savedPath));
    Assert("save creates .kanzu.json file", savedPath.EndsWith(".kanzu.json", StringComparison.OrdinalIgnoreCase));
    Assert("save file exists", File.Exists(savedPath));
    Assert("save file in drafts dir", Path.GetDirectoryName(savedPath) == draftsDir);
    var savedContent = File.ReadAllText(savedPath);
    AssertEqual("save content matches ToJson", json, savedContent);

    var savedPath2 = jsonDraft.Save(draftsDir);
    Assert("save creates unique files", savedPath != savedPath2);
    Assert("both files exist", File.Exists(savedPath) && File.Exists(savedPath2));

    AssertThrows<InvalidOperationException>("save invalid draft throws", () => new BeatmapDraft().Save(draftsDir));

    Console.WriteLine("Save - uses UTF8 encoding (no BOM):");
    var utf8Draft = new BeatmapDraft
    {
        Name = "UTF8 Test",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 3,
        Audio = new NewgroundsAudio(1, new string('a', 64))
    };
    var utf8Path = utf8Draft.Save(draftsDir);
    var utf8Bytes = await File.ReadAllBytesAsync(utf8Path);
    Assert("save file has no BOM", utf8Bytes.Length == 0 || utf8Bytes[0] != 0xEF || utf8Bytes.Length < 3 || utf8Bytes[1] != 0xBB || utf8Bytes[2] != 0xBF);
}
finally
{
    try { Directory.Delete(saveRoot, true); } catch { }
}

Console.WriteLine("Load:");
var loadRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var loadDir = Path.Combine(loadRoot, "drafts");
Directory.CreateDirectory(loadDir);
try
{
    var legacyJson = @"{
  ""name"": ""Legacy Map"",
  ""songTitle"": ""Legacy Song"",
  ""artist"": ""Legacy Artist"",
  ""difficulty"": ""Hard"",
  ""tags"": ""legacy"",
  ""description"": ""Old format"",
  ""backgroundVideoUrl"": """",
  ""audio"": {
    ""id"": 456,
    ""sha256"": ""ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff""
  },
  ""formatVersion"": 1
}";
    var legacyPath = Path.Combine(loadDir, "legacy.kanzu.json");
    File.WriteAllText(legacyPath, legacyJson);

    var loaded = BeatmapDraft.Load(legacyPath);
    AssertEqual("legacy name loaded", "Legacy Map", loaded.Name);
    AssertEqual("legacy songTitle loaded", "Legacy Song", loaded.SongTitle);
    AssertEqual("legacy artist loaded", "Legacy Artist", loaded.Artist);
    AssertEqual("legacy requestedStars defaults to 1", 1, loaded.RequestedStars);
    AssertEqual("legacy tags loaded", "legacy", loaded.Tags);
    AssertEqual("legacy description loaded", "Old format", loaded.Description);
    AssertEqual("legacy audio id loaded", 456, loaded.Audio.Id);
    AssertEqual("legacy audio sha256 loaded", "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", loaded.Audio.Sha256);
    AssertEqual("legacy formatVersion loaded", 1, loaded.FormatVersion);

    var modernJson = @"{
  ""name"": ""Modern Map"",
  ""songTitle"": ""Modern Song"",
  ""artist"": ""Modern Artist"",
  ""requestedStars"": 8,
  ""tags"": ""modern"",
  ""description"": ""New format"",
  ""backgroundVideoUrl"": ""https://example.com/vid.mp4"",
  ""audio"": {
    ""id"": 789,
    ""sha256"": ""eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee""
  },
  ""formatVersion"": 2
}";
    var modernPath = Path.Combine(loadDir, "modern.kanzu.json");
    File.WriteAllText(modernPath, modernJson);

    var loadedModern = BeatmapDraft.Load(modernPath);
    AssertEqual("modern name loaded", "Modern Map", loadedModern.Name);
    AssertEqual("modern requestedStars loaded", 8, loadedModern.RequestedStars);
    AssertEqual("modern formatVersion loaded", 2, loadedModern.FormatVersion);

    Console.WriteLine("Load - invalid JSON throws JsonException:");
    var invalidJsonPath = Path.Combine(loadDir, "invalid.kanzu.json");
    File.WriteAllText(invalidJsonPath, "{ not valid json }");
    AssertThrows<JsonException>("invalid json throws", () => BeatmapDraft.Load(invalidJsonPath));

    Console.WriteLine("Load - missing required name throws InvalidDataException:");
    var missingNameJson = @"{ ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 1, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" } }";
    var missingNamePath = Path.Combine(loadDir, "missing_name.kanzu.json");
    File.WriteAllText(missingNamePath, missingNameJson);
    AssertThrows<InvalidDataException>("missing name throws", () => BeatmapDraft.Load(missingNamePath));

    Console.WriteLine("Load - requestedStars 0 throws InvalidDataException:");
    var zeroStarsJson = @"{ ""name"": ""x"", ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 0, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" } }";
    var zeroStarsPath = Path.Combine(loadDir, "zero_stars.kanzu.json");
    File.WriteAllText(zeroStarsPath, zeroStarsJson);
    AssertThrows<InvalidDataException>("zero stars throws", () => BeatmapDraft.Load(zeroStarsPath));

    Console.WriteLine("Load - requestedStars 11 throws InvalidDataException:");
    var elevenStarsJson = @"{ ""name"": ""x"", ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 11, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" } }";
    var elevenStarsPath = Path.Combine(loadDir, "eleven_stars.kanzu.json");
    File.WriteAllText(elevenStarsPath, elevenStarsJson);
    AssertThrows<InvalidDataException>("eleven stars throws", () => BeatmapDraft.Load(elevenStarsPath));

    Console.WriteLine("Load - future formatVersion throws InvalidDataException:");
    var futureJson = @"{ ""name"": ""x"", ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 1, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" }, ""formatVersion"": 3 }";
    var futurePath = Path.Combine(loadDir, "future.kanzu.json");
    File.WriteAllText(futurePath, futureJson);
    AssertThrows<InvalidDataException>("future version throws", () => BeatmapDraft.Load(futurePath));

    Console.WriteLine("Load - file over 1MiB throws InvalidDataException:");
    var bigJson = @"{ ""name"": ""x"", ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 1, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" } }" + new string('x', 1024 * 1024);
    var bigPath = Path.Combine(loadDir, "big.kanzu.json");
    File.WriteAllText(bigPath, bigJson);
    AssertThrows<InvalidDataException>("oversized file throws", () => BeatmapDraft.Load(bigPath));

    Console.WriteLine("Load - case insensitive property names:");
    var caseJson = @"{ ""NAME"": ""Case Test"", ""SONGTITLE"": ""Song"", ""ARTIST"": ""Artist"", ""REQUESTEDSTARS"": 4, ""AUDIO"": { ""ID"": 999, ""SHA256"": ""bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"" } }";
    var casePath = Path.Combine(loadDir, "case.kanzu.json");
    File.WriteAllText(casePath, caseJson);
    var loadedCase = BeatmapDraft.Load(casePath);
    AssertEqual("case insensitive name", "Case Test", loadedCase.Name);
    AssertEqual("case insensitive requestedStars", 4, loadedCase.RequestedStars);
    AssertEqual("case insensitive audio id", 999, loadedCase.Audio.Id);
}
finally
{
    try { Directory.Delete(loadRoot, true); } catch { }
}

Console.WriteLine("Save with existingPath (atomic update):");
var updateRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var updateDir = Path.Combine(updateRoot, "drafts");
Directory.CreateDirectory(updateDir);
try
{
    var originalDraft = new BeatmapDraft
    {
        Name = "Original",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 2,
        Audio = new NewgroundsAudio(111, new string('1', 64))
    };
    var originalPath = originalDraft.Save(updateDir);
    var originalContent = File.ReadAllText(originalPath);
    Assert("original file exists", File.Exists(originalPath));

    var updatedDraft = new BeatmapDraft
    {
        Name = "Updated",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 9,
        Audio = new NewgroundsAudio(222, new string('2', 64))
    };
    var updatedPath = updatedDraft.Save(updateDir, originalPath);
    AssertEqual("returns same path", originalPath, updatedPath);
    Assert("original file still exists", File.Exists(originalPath));
    var updatedContent = File.ReadAllText(originalPath);
    Assert("content updated", updatedContent.Contains("\"name\": \"Updated\""));
    Assert("content has new stars", updatedContent.Contains("\"requestedStars\": 9"));
    Assert("content has new audio id", updatedContent.Contains("\"id\": 222"));

    var filesAfter = Directory.GetFiles(updateDir, "*.kanzu.json");
    AssertEqual("no duplicate files created", 1, filesAfter.Length);

    Console.WriteLine("Save existingPath - path safety (outside drafts dir throws):");
    var outsideDir = Path.Combine(updateRoot, "outside");
    Directory.CreateDirectory(outsideDir);
    var outsidePath = Path.Combine(outsideDir, "outside.kanzu.json");
    File.WriteAllText(outsidePath, originalContent);
    AssertThrows<InvalidOperationException>("outside path throws", () => updatedDraft.Save(updateDir, outsidePath));

    Console.WriteLine("Save existingPath - non .kanzu.json extension throws:");
    var badExtPath = Path.Combine(updateDir, "bad.txt");
    File.WriteAllText(badExtPath, originalContent);
    AssertThrows<InvalidOperationException>("bad extension throws", () => updatedDraft.Save(updateDir, badExtPath));
}
finally
{
    try { Directory.Delete(updateRoot, true); } catch { }
}

Console.WriteLine("Save existingPath - atomic temp cleanup on failure:");
var atomicRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var atomicDir = Path.Combine(atomicRoot, "drafts");
Directory.CreateDirectory(atomicDir);
try
{
    var draft1 = new BeatmapDraft
    {
        Name = "Test",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 1,
        Audio = new NewgroundsAudio(1, new string('a', 64))
    };
    var path1 = draft1.Save(atomicDir);
    var originalContent = File.ReadAllText(path1);

    var draft2 = new BeatmapDraft
    {
        Name = "Test",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 5,
        Audio = new NewgroundsAudio(2, new string('b', 64))
    };

    var tempFilesBefore = Directory.GetFiles(atomicDir, "*.tmp");
    AssertEqual("no temp files before", 0, tempFilesBefore.Length);

    try
    {
        var readonlyPath = path1;
        File.SetAttributes(readonlyPath, FileAttributes.ReadOnly);
        draft2.Save(atomicDir, readonlyPath);
        Console.WriteLine($"  FAIL: Save existingPath - should have thrown on readonly");
        failCount++;
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"  PASS: Save existingPath - readonly throws UnauthorizedAccessException");
        passCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL: Save existingPath - readonly threw {ex.GetType().Name}: {ex.Message}");
        failCount++;
    }
    finally
    {
        try { File.SetAttributes(path1, FileAttributes.Normal); } catch { }
    }

    var tempFilesAfter = Directory.GetFiles(atomicDir, "*.tmp");
    AssertEqual("no temp files left after failure", 0, tempFilesAfter.Length);

    var finalContent = File.ReadAllText(path1);
    AssertEqual("original file unchanged after failed save", originalContent, finalContent);
}
finally
{
    try { Directory.Delete(atomicRoot, true); } catch { }
}

Console.WriteLine("Save - LastWriteTimeUtc updated:");
var timeRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var timeDir = Path.Combine(timeRoot, "drafts");
Directory.CreateDirectory(timeDir);
try
{
    var timeDraft = new BeatmapDraft
    {
        Name = "Time Test",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 1,
        Audio = new NewgroundsAudio(1, new string('a', 64))
    };
    var timePath = timeDraft.Save(timeDir);
    var firstWriteTime = File.GetLastWriteTimeUtc(timePath);
    await Task.Delay(50);
    var timeDraft2 = new BeatmapDraft
    {
        Name = "Time Test 2",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 2,
        Audio = new NewgroundsAudio(1, new string('a', 64))
    };
    timeDraft2.Save(timeDir, timePath);
    var secondWriteTime = File.GetLastWriteTimeUtc(timePath);
    Assert("LastWriteTimeUtc updated on save", secondWriteTime > firstWriteTime);
}
finally
{
    try { Directory.Delete(timeRoot, true); } catch { }
}

Console.WriteLine("Roundtrip Save/Load:");
var rtRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var rtDir = Path.Combine(rtRoot, "drafts");
Directory.CreateDirectory(rtDir);
try
{
    var rtDraft = new BeatmapDraft
    {
        Name = "Roundtrip",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 6,
        Tags = "tag1,tag2",
        Description = "Roundtrip test",
        BackgroundVideoUrl = "https://example.com/video.mp4",
        Audio = new NewgroundsAudio(333, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
        FormatVersion = 2
    };
    var rtPath = rtDraft.Save(rtDir);
    var rtLoaded = BeatmapDraft.Load(rtPath);
    AssertEqual("roundtrip name", rtDraft.Name, rtLoaded.Name);
    AssertEqual("roundtrip songTitle", rtDraft.SongTitle, rtLoaded.SongTitle);
    AssertEqual("roundtrip artist", rtDraft.Artist, rtLoaded.Artist);
    AssertEqual("roundtrip requestedStars", rtDraft.RequestedStars, rtLoaded.RequestedStars);
    AssertEqual("roundtrip tags", rtDraft.Tags, rtLoaded.Tags);
    AssertEqual("roundtrip description", rtDraft.Description, rtLoaded.Description);
    AssertEqual("roundtrip backgroundVideoUrl", rtDraft.BackgroundVideoUrl, rtLoaded.BackgroundVideoUrl);
    AssertEqual("roundtrip audio id", rtDraft.Audio.Id, rtLoaded.Audio.Id);
    AssertEqual("roundtrip audio sha256", rtDraft.Audio.Sha256, rtLoaded.Audio.Sha256);
    AssertEqual("roundtrip formatVersion", rtDraft.FormatVersion, rtLoaded.FormatVersion);
}
finally
{
    try { Directory.Delete(rtRoot, true); } catch { }
}

Console.WriteLine("ProjectStore.Load:");
var psRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
var psDir = Path.Combine(psRoot, "drafts");
Directory.CreateDirectory(psDir);
try
{
    Console.WriteLine("ProjectStore.Load - missing directory returns empty:");
    var missingDir = Path.Combine(psRoot, "missing");
    var missingResult = ProjectStore.Load(missingDir, out var missingUnreadable);
    AssertEqual("missing dir returns empty list", 0, missingResult.Count);
    AssertEqual("missing dir unreadable count 0", 0, missingUnreadable);

    Console.WriteLine("ProjectStore.Load - loads valid projects:");
    var draftA = new BeatmapDraft { Name = "A", SongTitle = "S", Artist = "Art", RequestedStars = 1, Audio = new NewgroundsAudio(1, new string('a', 64)) };
    var draftB = new BeatmapDraft { Name = "B", SongTitle = "S", Artist = "Art", RequestedStars = 2, Audio = new NewgroundsAudio(2, new string('b', 64)) };
    var draftC = new BeatmapDraft { Name = "C", SongTitle = "S", Artist = "Art", RequestedStars = 3, Audio = new NewgroundsAudio(3, new string('c', 64)) };
    var pathA = draftA.Save(psDir);
    await Task.Delay(50);
    var pathB = draftB.Save(psDir);
    await Task.Delay(50);
    var pathC = draftC.Save(psDir);

    var result = ProjectStore.Load(psDir, out var unreadable);
    AssertEqual("loads 3 projects", 3, result.Count);
    AssertEqual("unreadable count 0", 0, unreadable);
    AssertEqual("newest first (C)", "C", result[0].Draft.Name);
    AssertEqual("middle (B)", "B", result[1].Draft.Name);
    AssertEqual("oldest last (A)", "A", result[2].Draft.Name);
    AssertEqual("path tie-break deterministic", pathC, result[0].Path);
    AssertEqual("updatedAt is file write time", File.GetLastWriteTimeUtc(pathC), result[0].UpdatedAt);

    Console.WriteLine("ProjectStore.Load - skips malformed files and counts them:");
    var badPath = Path.Combine(psDir, "bad.kanzu.json");
    File.WriteAllText(badPath, "{ invalid json }");
    var result2 = ProjectStore.Load(psDir, out var unreadable2);
    AssertEqual("still loads 3 valid projects", 3, result2.Count);
    AssertEqual("unreadable count 1", 1, unreadable2);

    Console.WriteLine("ProjectStore.Load - skips invalid draft files and counts them:");
    var invalidDraftPath = Path.Combine(psDir, "invalid_draft.kanzu.json");
    File.WriteAllText(invalidDraftPath, @"{ ""name"": """", ""songTitle"": ""x"", ""artist"": ""x"", ""requestedStars"": 1, ""audio"": { ""id"": 1, ""sha256"": ""aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"" } }");
    var result3 = ProjectStore.Load(psDir, out var unreadable3);
    AssertEqual("still loads 3 valid projects", 3, result3.Count);
    AssertEqual("unreadable count 2", 2, unreadable3);

    Console.WriteLine("ProjectStore.Load - does not merge distinct GUID files:");
    var draftD = new BeatmapDraft { Name = "D", SongTitle = "S", Artist = "Art", RequestedStars = 4, Audio = new NewgroundsAudio(4, new string('d', 64)) };
    var pathD = draftD.Save(psDir);
    var result4 = ProjectStore.Load(psDir, out var unreadable4);
    AssertEqual("loads 4 distinct projects", 4, result4.Count);
    AssertEqual("unreadable count still 2", 2, unreadable4);

    Console.WriteLine("ProjectStore.Load - ignores non-.kanzu.json files:");
    var txtPath = Path.Combine(psDir, "readme.txt");
    File.WriteAllText(txtPath, "not a project");
    var result5 = ProjectStore.Load(psDir, out var unreadable5);
    AssertEqual("still loads 4 projects", 4, result5.Count);
    AssertEqual("unreadable count unchanged", 2, unreadable5);
}
finally
{
    try { Directory.Delete(psRoot, true); } catch { }
}

Console.WriteLine("\n=== Hold confirmation tests ===");
var hold = new HoldConfirmation();
Assert("hold cannot begin without a new press", !hold.Update(2f, false, true, 1f));
AssertEqual("unstarted hold has no progress", 0f, hold.Progress);
Assert("a click does not confirm", !hold.Update(2f, true, true, 1f));
AssertEqual("press frame does not include earlier frame time", 0f, hold.Progress);
Assert("half-second hold does not confirm", !hold.Update(0.5f, false, true, 1f));
AssertEqual("half-second progress", 0.5f, hold.Progress);
Assert("visual fill advances faster at the start", hold.VisualProgress > hold.Progress);
Assert("visual fill slows near the end", hold.VisualProgress < 1f);
Assert("release cancels confirmation", !hold.Update(0.1f, false, false, 1f));
AssertEqual("release resets progress", 0f, hold.Progress);
AssertEqual("release resets visual fill", 0f, hold.VisualProgress);
Assert("entering while already held cannot confirm", !hold.Update(2f, false, true, 1f));
Assert("new press starts a new hold", !hold.Update(0f, true, true, 1f));
Assert("less than one second cannot confirm", !hold.Update(0.99f, false, true, 1f));
Assert("one continuous second confirms", hold.Update(0.02f, false, true, 1f));
AssertEqual("completed progress is clamped", 1f, hold.Progress);
Assert("confirmation fires only once", !hold.Update(2f, false, true, 1f));
hold.Reset();
AssertEqual("popup reset clears progress", 0f, hold.Progress);
Assert("popup reset prevents stale hold confirmation", !hold.Update(2f, false, true, 1f));
hold.Update(0f, true, true, 1f);
hold.Update(-2f, false, true, 1f);
AssertEqual("negative frame time cannot advance progress", 0f, hold.Progress);

Console.WriteLine("\n=== Star slider tests ===");
AssertEqual("slider starts at one", 1, new StarSlider().Value);
AssertEqual("left endpoint is one", 1, StarSlider.ValueAt(0f));
AssertEqual("right endpoint is ten", 10, StarSlider.ValueAt(1f));
AssertEqual("position below range clamps to one", 1, StarSlider.ValueAt(-1f));
AssertEqual("position above range clamps to ten", 10, StarSlider.ValueAt(2f));
for (int stars = 1; stars <= 10; stars++)
{
    AssertEqual($"slider snaps to {stars}", stars, StarSlider.ValueAt((stars - 1f) / 9f));
}
var slider = new StarSlider { Value = 20 };
AssertEqual("assigned stars clamp to ten", 10, slider.Value);
slider.Value = -1;
AssertEqual("assigned stars clamp to one", 1, slider.Value);

Console.WriteLine("\n=== Project deletion tests ===");
string deleteRoot = Path.Combine(Path.GetTempPath(), "KanzuTests", Guid.NewGuid().ToString("N"));
string deleteDirectory = Path.Combine(deleteRoot, "Drafts");
string audioDirectory = Path.Combine(deleteRoot, "Audio");
Directory.CreateDirectory(audioDirectory);
try
{
    var first = new BeatmapDraft
    {
        Name = "Delete me",
        SongTitle = "Song",
        Artist = "Artist",
        RequestedStars = 4,
        Audio = new NewgroundsAudio(1, new string('a', 64))
    };
    string selectedPath = first.Save(deleteDirectory);
    string otherPath = (first with { Name = "Keep me" }).Save(deleteDirectory);
    string audioPath = Path.Combine(audioDirectory, "shared.ogg");
    File.WriteAllText(audioPath, "shared audio fixture");
    string outsidePath = Path.Combine(deleteRoot, "outside.kanzu.json");
    File.WriteAllText(outsidePath, first.ToJson());
    string otherFile = Path.Combine(deleteDirectory, "notes.txt");
    File.WriteAllText(otherFile, "Keep these notes");

    AssertThrows<InvalidOperationException>("cannot delete outside the project folder",
        () => ProjectStore.Delete(deleteDirectory, outsidePath));
    Assert("outside file remains", File.Exists(outsidePath));
    AssertThrows<InvalidOperationException>("cannot delete an unrelated file",
        () => ProjectStore.Delete(deleteDirectory, otherFile));
    Assert("unrelated file remains", File.Exists(otherFile));
    ProjectStore.Delete(deleteDirectory, selectedPath);
    Assert("selected map is deleted", !File.Exists(selectedPath));
    Assert("other map remains", File.Exists(otherPath));
    Assert("shared cached audio remains", File.Exists(audioPath));
    var remaining = ProjectStore.Load(deleteDirectory, out int unreadableCount);
    AssertEqual("deleted map disappears from the project list", 1, remaining.Count);
    AssertEqual("remaining map keeps its name", "Keep me", remaining[0].Draft.Name);
    AssertEqual("deletion does not produce unreadable files", 0, unreadableCount);
    AssertThrows<FileNotFoundException>("already deleted map reports a missing file",
        () => ProjectStore.Delete(deleteDirectory, selectedPath));
}
finally
{
    Directory.Delete(deleteRoot, recursive: true);
}

Console.WriteLine("\n=== Summary ===");
Console.WriteLine($"Passed: {passCount}");
Console.WriteLine($"Failed: {failCount}");
if (failCount > 0)
{
    Environment.Exit(1);
}

static string ComputeSha256Hex(byte[] bytes)
{
    using var sha256 = SHA256.Create();
    var hash = sha256.ComputeHash(bytes);
    var sb = new StringBuilder(hash.Length * 2);
    foreach (var b in hash)
    {
        sb.Append(b.ToString("x2"));
    }
    return sb.ToString();
}