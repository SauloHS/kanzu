using System.Reflection;
using System.Runtime.InteropServices;
using Kanzu;
using Raylib_cs;

internal static class NativeEditorSmoke
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    public static unsafe void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string root = Path.Combine(Path.GetTempPath(), "KanzuNativeTests", Guid.NewGuid().ToString("N"));
        string drafts = Path.Combine(root, "Drafts");
        Directory.CreateDirectory(drafts);
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(1280, 720, "Kanzu native editor tests");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);
        Raylib.InitAudioDevice();
        AppFont.Load();
        Sfx.Load();
        try
        {
            var game = new Game();
            var library = new ProjectLibraryScreen(game);
            Set(library, "draftsDirectory", drafts);
            library.Load();
            Capture(library, Path.Combine(outputDirectory, "projects-empty.png"));
            var empty = new EditorScreen(game);
            Set(empty, "storageRoot", root);
            empty.Load();
            VerifyDownloadControls(empty, root);
            Capture(empty, Path.Combine(outputDirectory, "setup-new.png"));
            Call(empty, "RequestExit");
            Capture(empty, Path.Combine(outputDirectory, "save-popup.png"));
            Call(empty, "CloseExitPopup");
            empty.Unload();

            string fixture = Path.Combine(AppContext.BaseDirectory, "Assets", "sfx", "click.ogg");
            string hash = LocalAudio.ComputeHashAsync(fixture).GetAwaiter().GetResult();
            BeatmapDraft draft = new()
            {
                Name = "Native test project",
                SongTitle = "Audio import fixture",
                Artist = "Test artist",
                RequestedStars = 7,
                Description = "Native rendering and project reopening test.",
                Audio = new NewgroundsAudio(1, hash)
            };
            string path = draft.Save(drafts);
            var project = new SavedProject(path, draft, File.GetLastWriteTimeUtc(path));
            var editor = new EditorScreen(game, project);
            Set(editor, "storageRoot", root);
            editor.Load();
            Require((bool)Call(editor, "IsSaved")!, "Reopened project is saved before editing.");
            Call(editor, "StartImport", fixture);
            Get<Task<ImportedAudio>>(editor, "importTask").GetAwaiter().GetResult();
            Call(editor, "CompleteImport");
            Require(Get<bool>(editor, "hasMusic"), "Imported audio decodes in Raylib.");
            Require(Get<ImportedAudio>(editor, "importedAudio").Sha256 == hash, "Imported hash matches the saved project.");
            Get<StarSlider>(editor, "starsSlider").Value = 10;
            Require(!(bool)Call(editor, "IsSaved")!, "Changing requested stars marks the project unsaved.");
            Require((bool)Call(editor, "SaveDraft")!, "Edited project saves.");
            Require(Directory.GetFiles(drafts, "*.kanzu.json").Length == 1, "Saving an edit does not duplicate the project.");
            Require(BeatmapDraft.Load(path).RequestedStars == 10, "Requested stars persist.");
            Capture(editor, Path.Combine(outputDirectory, "setup-saved.png"));

            nint window = (nint)Raylib.GetWindowHandle();
            PostMessage(window, 0x0007, 0, 0);
            Pump(editor);
            TextField name = Get<TextField>(editor, "nameField");
            int x = (int)(name.Bounds.X + name.Bounds.Width / 2f);
            int y = (int)(name.Bounds.Y + name.Bounds.Height / 2f);
            PostMessage(window, 0x0200, 0, (nint)((y << 16) | (x & 0xffff)));
            Pump(editor);
            editor.Update();
            bool hovered = Get<bool>(name, "hovering");
            Console.WriteLine(hovered ? "PASS: native text-input hover" : "SKIP: hidden window cannot receive focus; hover needs a visible window.");
            Capture(editor, Path.Combine(outputDirectory, "setup-hover.png"));
            editor.Unload();

            var recent = draft with { Name = "Most recent native test", RequestedStars = 4 };
            recent.Save(drafts);
            library.Load();
            Capture(library, Path.Combine(outputDirectory, "projects-saved.png"));
            var listed = Get<List<SavedProject>>(library, "projects");
            Require(listed.Count == 2, "Both test projects are listed.");
            SavedProject selected = listed[0];
            Call(library, "RequestDeletion", selected);
            Require(File.Exists(selected.Path), "Opening the confirmation does not delete the project.");
            Capture(library, Path.Combine(outputDirectory, "delete-confirmation.png"));
            Set(library, "pendingDeletion", null!);
            Require(File.Exists(selected.Path), "Canceling deletion leaves the project intact.");
            Call(library, "RequestDeletion", selected);
            Call(library, "DeleteSelectedProject");
            Require(!File.Exists(selected.Path), "Confirmation deletes the selected project.");
            Require(File.Exists(path), "The other project remains.");
            Require(Directory.GetFiles(Path.Combine(root, "Audio"), "*.ogg").Length == 1,
                "Deleting a project preserves cached audio.");
            Require(Get<List<SavedProject>>(library, "projects").Count == 1,
                "The library refreshes after deletion.");
            Capture(library, Path.Combine(outputDirectory, "projects-after-delete.png"));
            Raylib.SetWindowSize(1920, 1080);
            library.Update();
            Capture(library, Path.Combine(outputDirectory, "projects-1920.png"));
            library.Unload();
            Console.WriteLine("PASS: native editor rendering, reopen, audio decode, and in-place save.");
        }
        finally
        {
            Sfx.Unload();
            AppFont.Unload();
            Raylib.CloseAudioDevice();
            Raylib.CloseWindow();
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyDownloadControls(EditorScreen editor, string root)
    {
        EditorButton button = Get<EditorButton>(editor, "importButton");
        TextField id = Get<TextField>(editor, "idField");
        TextField title = Get<TextField>(editor, "titleField");
        TextField artist = Get<TextField>(editor, "artistField");
        Require(button.Label == "Download audio", "Download is the default audio action.");
        Require(!button.Enabled, "Download requires a valid audio ID.");
        id.Text = "123";
        Call(editor, "UpdateAudioId");
        Call(editor, "UpdateValidation");
        Require(button.Enabled, "A valid ID enables downloading.");
        for (int frame = 0; frame < 120; frame++)
        {
            editor.Update();
        }

        Require(Get<Task<ImportedAudio>?>(editor, "importTask") == null,
            "Entering an ID and waiting never starts a download.");
        Set(editor, "downloadingAudio", true);
        Set(editor, "importingId", 123L);
        Set(editor, "importTask", Task.FromException<ImportedAudio>(new SongDownloadException(400)));
        Call(editor, "UpdateValidation");
        Require(button.Label == "Downloading..." && !button.Enabled, "Download action is disabled while downloading.");
        Call(editor, "CompleteImport");
        Call(editor, "UpdateValidation");
        Require(button.Label == "Choose audio file" && button.Enabled,
            "Manual import appears only after downloading fails.");
        id.Text = "456";
        Call(editor, "UpdateAudioId");
        Call(editor, "UpdateValidation");
        Require(button.Label == "Download audio", "Changing the ID clears the manual fallback.");

        string source = Path.Combine(root, "metadata.ogg");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "sfx", "click.ogg"), source);
        using (var taggedFile = TagLib.File.Create(source))
        {
            taggedFile.Tag.Title = "Fixture Song (ID: 456)";
            taggedFile.Tag.Performers = ["Fixture Artist"];
            taggedFile.Save();
        }

        Call(editor, "BeginImport");
        Set(editor, "downloadingAudio", true);
        Set(editor, "importTask", LocalAudio.ImportAsync(source, Path.Combine(root, "MetadataAudio")));
        Get<Task<ImportedAudio>>(editor, "importTask").GetAwaiter().GetResult();
        Call(editor, "CompleteImport");
        Call(editor, "UpdateValidation");
        Require(title.Text == "Fixture Song", "Downloaded title populates without the Newgrounds ID suffix.");
        Require(artist.Text == "Fixture Artist", "Downloaded artist populates from embedded tags.");
        Require(button.Label == "Download audio", "A successful download does not expose manual import.");
        Require(Get<bool>(editor, "hasMusic"), "Downloaded audio supports preview.");

        Call(editor, "BeginImport");
        Set(editor, "downloadingAudio", true);
        Set(editor, "importTask", LocalAudio.ImportAsync(source, Path.Combine(root, "MetadataAudio")));
        title.Text = "User title";
        artist.Text = "User artist";
        Get<Task<ImportedAudio>>(editor, "importTask").GetAwaiter().GetResult();
        Call(editor, "CompleteImport");
        Require(title.Text == "User title" && artist.Text == "User artist",
            "Download completion preserves text edited while downloading.");

        Call(editor, "BeginImport");
        Set(editor, "downloadingAudio", true);
        var oldDownload = new TaskCompletionSource<ImportedAudio>();
        Set(editor, "importTask", oldDownload.Task);
        id.Text = "789";
        Call(editor, "UpdateAudioId");
        oldDownload.SetResult(new ImportedAudio("456.mp3", new string('a', 64), source, 1, false)
        {
            Title = "Old Song (ID: 456)", Artist = "Old Artist"
        });
        Call(editor, "CompleteImport");
        Require(title.Text == "User title" && artist.Text == "User artist",
            "A stale download cannot overwrite the newer ID's fields.");
        id.Text = "";
        title.Text = "";
        artist.Text = "";
        Call(editor, "UpdateAudioId");
        Call(editor, "UpdateValidation");
        Console.WriteLine("PASS: native download controls, failure fallback, metadata autofill, and stale-download isolation.");
    }

    private static void Pump(IScreen screen)
    {
        Raylib.BeginDrawing();
        screen.Draw();
        Raylib.EndDrawing();
    }

    private static void Capture(IScreen screen, string path)
    {
        RenderTexture2D target = Raylib.LoadRenderTexture(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        Raylib.BeginTextureMode(target);
        screen.Draw();
        Raylib.EndTextureMode();
        Image image = Raylib.LoadImageFromTexture(target.Texture);
        Raylib.ImageFlipVertical(ref image);
        Require(Raylib.ExportImage(image, path), "Native screenshot saved.");
        Raylib.UnloadImage(image);
        Raylib.UnloadRenderTexture(target);
    }

    private static T Get<T>(object value, string name)
    {
        return (T)value.GetType().GetField(name, Fields)!.GetValue(value)!;
    }

    private static void Set(object value, string name, object data)
    {
        value.GetType().GetField(name, Fields)!.SetValue(value, data);
    }

    private static object? Call(object value, string name, params object[] arguments)
    {
        return value.GetType().GetMethod(name, Fields)!.Invoke(value, arguments);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
