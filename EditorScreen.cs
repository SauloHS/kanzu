using System.ComponentModel;
using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public class EditorScreen : IScreen
{
    private readonly Game game;
    private readonly TextField nameField = new("Name your level", 48);
    private readonly TextField titleField = new("Song title", 100);
    private readonly TextField artistField = new("Original artist", 100);
    private readonly StarSlider starsSlider = new();
    private readonly TextField tagsField = new("Search tags, separated by spaces", 160);
    private readonly TextField descriptionField = new("Tell players about this level", 500, multiline: true);
    private readonly TextField videoField = new("https://...", 512);
    private readonly TextField idField = new("Audio ID", 19) { DigitsOnly = true };
    private readonly EditorButton pageButton = new("Open Newgrounds page");
    private readonly EditorButton importButton = new("Choose audio file");
    private readonly EditorButton previewButton = new("Preview audio");
    private readonly EditorButton copyButton = new("Copy hash");
    private readonly MenuButton[] exitButtons;
    private readonly TextField[] fields;
    private readonly string storageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kanzu");

    private Task<ImportedAudio>? importTask;
    private CancellationTokenSource? importCancellation;
    private ImportedAudio? importedAudio;
    private BeatmapDraft? savedDraft;
    private string? savedPath;
    private string savedInput = "";
    private bool exitPopupOpen;
    private Rectangle popupBounds;
    private Texture2D saveIcon;
    private float saveIconRetry;
    private string popupError = "";
    private Music music;
    private bool hasMusic;
    private bool previewStarted;
    private long selectedId;
    private long importingId;
    private float duration;
    private float scale;
    private Rectangle detailsPanel;
    private Rectangle audioPanel;
    private Rectangle dropArea;
    private string importError = "";
    private string footerMessage = "";
    private bool footerError;
    private bool showValidation;
    private bool dropHovered;
    private string expectedAudioHash = "";
    private bool restoringAudio;
    private int keyboardFocus = -1;
    private object[] focusOrder = [];

    public EditorScreen(Game game)
    {
        this.game = game;
        fields = [nameField, titleField, artistField, tagsField, descriptionField, videoField, idField];
        exitButtons =
        [
            new MenuButton("Save and go back to main menu", "", new Color(50, 132, 100, 255), SaveAndReturn),
            new MenuButton("Continue editing", "", new Color(166, 91, 31, 255), CloseExitPopup),
            new MenuButton("Quit without saving", "", new Color(173, 55, 78, 255), ReturnToMenu) { HoldSeconds = 1f }
        ];
    }

    public EditorScreen(Game game, SavedProject project) : this(game)
    {
        BeatmapDraft draft = project.Draft;
        nameField.Text = draft.Name;
        titleField.Text = draft.SongTitle;
        artistField.Text = draft.Artist;
        starsSlider.Value = draft.RequestedStars;
        tagsField.Text = draft.Tags;
        descriptionField.Text = draft.Description;
        videoField.Text = draft.BackgroundVideoUrl;
        idField.Text = draft.Audio.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        selectedId = draft.Audio.Id;
        expectedAudioHash = draft.Audio.Sha256;
        savedDraft = BuildDraft();
        savedPath = project.Path;
        savedInput = InputSnapshot();
    }

    public void Load()
    {
        Layout();
        RestoreCachedAudio();
        TryLoadSaveIcon();
        foreach (MenuButton button in exitButtons)
        {
            button.Load();
        }
    }

    public void Unload()
    {
        CancelImport();
        UnloadAudio();
        foreach (MenuButton button in exitButtons)
        {
            button.Unload();
        }

        if (saveIcon.Id != 0)
        {
            Raylib.UnloadTexture(saveIcon);
            saveIcon = default;
        }
    }

    public void Update()
    {
        Layout();
        float deltaTime = Raylib.GetFrameTime();
        if (exitPopupOpen)
        {
            CompleteImport();
            if (hasMusic)
            {
                Raylib.UpdateMusicStream(music);
            }

            UpdateExitPopup(deltaTime);
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            CompleteImport();
            RequestExit();
            return;
        }

        UpdateFocus();
        foreach (TextField field in fields)
        {
            field.Update(deltaTime);
        }

        UpdateAudioId();
        starsSlider.Update();
        UpdateDropHover();
        CompleteImport();
        if (hasMusic)
        {
            Raylib.UpdateMusicStream(music);
        }

        UpdateValidation();
        if (pageButton.Update())
        {
            Raylib.MinimizeWindow();
            Raylib.OpenURL(new NewgroundsAudio(selectedId, "").PageUrl);
            return;
        }

        if (importButton.Update())
        {
            ChooseAudio();
        }

        if (previewButton.Update())
        {
            TogglePreview();
        }

        if (copyButton.Update())
        {
            Raylib.SetClipboardText(importedAudio?.Sha256 ?? expectedAudioHash);
            footerMessage = "Hash copied.";
            footerError = false;
        }

        bool control = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (control && Raylib.IsKeyPressed(KeyboardKey.S))
        {
            SaveDraft();
        }

        if (Raylib.IsFileDropped())
        {
            string[] files = Raylib.GetDroppedFiles();
            if (files.Length != 1)
            {
                importError = "Drop one MP3, OGG, or WAV file at a time.";
            }
            else
            {
                StartImport(files[0]);
            }
        }

        previewButton.Label = hasMusic && Raylib.IsMusicStreamPlaying(music) ? "Pause preview" : "Preview audio";
    }

    private void Layout()
    {
        float width = Raylib.GetScreenWidth();
        float height = Raylib.GetScreenHeight();
        scale = MathF.Min(width / 1280f, height / 780f);
        float totalWidth = MathF.Min(width - 80f * scale, 1320f * scale);
        float x = (width - totalWidth) / 2f;
        float gap = 24f * scale;
        float leftWidth = (totalWidth - gap) * 0.6f;
        float top = 116f * scale;
        float panelHeight = height - top - 60f * scale;
        detailsPanel = new Rectangle(x, top, leftWidth, panelHeight);
        audioPanel = new Rectangle(x + leftWidth + gap, top, totalWidth - leftWidth - gap, panelHeight);

        float padding = 24f * scale;
        float fieldHeight = 40f * scale;
        float left = detailsPanel.X + padding;
        float innerWidth = detailsPanel.Width - padding * 2f;
        float halfWidth = (innerWidth - 20f * scale) / 2f;
        nameField.Bounds = new Rectangle(left, top + 82f * scale, innerWidth, fieldHeight);
        titleField.Bounds = new Rectangle(left, top + 154f * scale, halfWidth, fieldHeight);
        artistField.Bounds = new Rectangle(left + halfWidth + 20f * scale, titleField.Bounds.Y, halfWidth, fieldHeight);
        starsSlider.Bounds = new Rectangle(left, top + 226f * scale, halfWidth, fieldHeight);
        tagsField.Bounds = new Rectangle(artistField.Bounds.X, starsSlider.Bounds.Y, halfWidth, fieldHeight);
        descriptionField.Bounds = new Rectangle(left, top + 298f * scale, innerWidth, 92f * scale);
        videoField.Bounds = new Rectangle(left, top + 436f * scale, innerWidth, fieldHeight);

        float right = audioPanel.X + padding;
        float audioWidth = audioPanel.Width - padding * 2f;
        idField.Bounds = new Rectangle(right, top + 82f * scale, audioWidth, fieldHeight);
        pageButton.Bounds = new Rectangle(right, top + 134f * scale, audioWidth, 36f * scale);
        dropArea = new Rectangle(right, top + 232f * scale, audioWidth, 84f * scale);
        importButton.Bounds = new Rectangle(right + 12f * scale, dropArea.Y + 10f * scale,
            audioWidth - 24f * scale, 38f * scale);
        float buttonWidth = (audioWidth - 12f * scale) / 2f;
        float actionTop = audioPanel.Y + audioPanel.Height - 56f * scale;
        previewButton.Bounds = new Rectangle(right, actionTop, buttonWidth, 36f * scale);
        copyButton.Bounds = new Rectangle(right + buttonWidth + 12f * scale, actionTop, buttonWidth, 36f * scale);

        foreach (TextField field in fields)
        {
            field.FontSize = 18f * scale;
            field.Padding = 12f * scale;
        }

        focusOrder = [nameField, titleField, artistField, starsSlider, tagsField, descriptionField,
            videoField, idField, pageButton, importButton, previewButton, copyButton];

        float popupWidth = 660f * scale;
        float popupHeight = 600f * scale;
        popupBounds = new Rectangle((width - popupWidth) / 2f, (height - popupHeight) / 2f, popupWidth, popupHeight);
        for (int i = 0; i < exitButtons.Length; i++)
        {
            exitButtons[i].Bounds = new Rectangle(popupBounds.X + 48f * scale,
                popupBounds.Y + (214f + i * 106f) * scale, popupWidth - 96f * scale, 88f * scale);
        }
    }

    private void UpdateFocus()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            keyboardFocus = -1;
            for (int i = 0; i < focusOrder.Length; i++)
            {
                Rectangle bounds = focusOrder[i] switch
                {
                    TextField field => field.Bounds,
                    StarSlider slider => slider.Bounds,
                    EditorButton button => button.Bounds,
                    _ => default
                };
                if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), bounds))
                {
                    keyboardFocus = i;
                }
            }

            ApplyFocus();
        }

        if (!Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            return;
        }

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        int direction = shift ? -1 : 1;
        for (int i = 0; i < focusOrder.Length; i++)
        {
            keyboardFocus = (keyboardFocus + direction + focusOrder.Length) % focusOrder.Length;
            if (focusOrder[keyboardFocus] is not EditorButton button || button.Enabled)
            {
                break;
            }
        }

        ApplyFocus();
    }

    private void ApplyFocus()
    {
        for (int i = 0; i < focusOrder.Length; i++)
        {
            if (focusOrder[i] is TextField field)
            {
                field.Focus(i == keyboardFocus);
            }
            else if (focusOrder[i] is StarSlider slider)
            {
                slider.Focused = i == keyboardFocus;
            }
            else
            {
                ((EditorButton)focusOrder[i]).Focused = i == keyboardFocus;
            }
        }
    }

    private void UpdateAudioId()
    {
        NewgroundsAudio.TryParseId(idField.Text, out long id);
        if (id == selectedId)
        {
            return;
        }

        bool hadAudio = importedAudio != null || importTask != null;
        selectedId = id;
        expectedAudioHash = "";
        restoringAudio = false;
        CancelImport();
        UnloadAudio();
        importedAudio = null;
        importError = hadAudio ? "Audio ID changed. Import the original song again." : "";
        footerMessage = "";
    }

    private void UpdateDropHover()
    {
        bool hovering = Raylib.IsWindowFocused() &&
            Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), dropArea) &&
            !Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), importButton.Bounds);
        if (hovering && !dropHovered)
        {
            Sfx.PlayHover();
        }

        dropHovered = hovering;
        if (hovering && Raylib.IsMouseButtonPressed(MouseButton.Left) && importButton.Enabled)
        {
            Sfx.PlayClick();
            ChooseAudio();
        }
    }

    private void RestoreCachedAudio()
    {
        if (!NewgroundsAudio.IsValidHash(expectedAudioHash))
        {
            return;
        }

        string directory = Path.Combine(storageRoot, "Audio");
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            string? path = Directory.EnumerateFiles(directory, expectedAudioHash.ToLowerInvariant() + ".*")
                .FirstOrDefault(LocalAudio.IsSupportedFile);
            if (path != null)
            {
                StartImport(path);
                restoringAudio = true;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            importError = "Cached audio could not be opened. Import the original file again.";
        }
    }

    private void ChooseAudio()
    {
        try
        {
            string? path = NativeFilePicker.OpenAudio();
            if (path != null)
            {
                StartImport(path);
            }
        }
        catch (Exception error) when (error is Win32Exception or PlatformNotSupportedException)
        {
            importError = error.Message;
        }
    }

    private void StartImport(string path)
    {
        if (selectedId <= 0)
        {
            importError = "Enter a valid Newgrounds audio ID before importing.";
            return;
        }

        if (!LocalAudio.IsSupportedFile(path))
        {
            importError = "Unsupported file. Choose an MP3, OGG, or WAV.";
            return;
        }

        CancelImport();
        UnloadAudio();
        restoringAudio = false;
        importedAudio = null;
        importError = "";
        footerMessage = "";
        importingId = selectedId;
        importCancellation = new CancellationTokenSource();
        importTask = LocalAudio.ImportAsync(path, Path.Combine(storageRoot, "Audio"), importCancellation.Token);
    }

    private void CancelImport()
    {
        importCancellation?.Cancel();
        importCancellation?.Dispose();
        importCancellation = null;
        if (importTask != null)
        {
            _ = importTask.ContinueWith(task => _ = task.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            importTask = null;
        }
    }

    private void CompleteImport()
    {
        if (importTask == null || !importTask.IsCompleted)
        {
            return;
        }

        Task<ImportedAudio> completed = importTask;
        importTask = null;
        importCancellation?.Dispose();
        importCancellation = null;
        try
        {
            ImportedAudio result = completed.GetAwaiter().GetResult();
            if (importingId != selectedId)
            {
                return;
            }

            if (NewgroundsAudio.IsValidHash(expectedAudioHash) &&
                !NewgroundsAudio.MatchesHash(expectedAudioHash, result.Sha256))
            {
                importError = "Audio does not match this project's hash. Import the original file.";
                return;
            }

            Music candidate = Raylib.LoadMusicStream(result.LocalPath);
            bool valid = Raylib.IsMusicValid(candidate);
            float seconds = valid ? Raylib.GetMusicTimeLength(candidate) : 0f;
            if (!valid || !float.IsFinite(seconds) || seconds <= 0f)
            {
                if (valid)
                {
                    Raylib.UnloadMusicStream(candidate);
                }

                if (result.CreatedCacheFile)
                {
                    File.Delete(result.LocalPath);
                }

                importError = "This file could not be decoded. Import the original download, not a renamed or converted file.";
                return;
            }

            music = candidate;
            hasMusic = true;
            duration = seconds;
            importedAudio = restoringAudio ? result with { FileName = "Cached audio" } : result;
            Raylib.SetMusicVolume(music, 0.65f);
        }
        catch (OperationCanceledException)
        {
            importError = "Import canceled. Choose the audio file again.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            importError = "Import failed. Check that the file is readable and try again. " + error.Message;
        }
    }

    private void UnloadAudio()
    {
        if (!hasMusic)
        {
            return;
        }

        Raylib.UnloadMusicStream(music);
        hasMusic = false;
        previewStarted = false;
        duration = 0f;
    }

    private void TogglePreview()
    {
        if (!hasMusic)
        {
            return;
        }

        if (Raylib.IsMusicStreamPlaying(music))
        {
            Raylib.PauseMusicStream(music);
            return;
        }

        if (previewStarted)
        {
            Raylib.ResumeMusicStream(music);
        }
        else
        {
            Raylib.PlayMusicStream(music);
            previewStarted = true;
        }
    }

    private BeatmapDraft BuildDraft()
    {
        return new BeatmapDraft
        {
            Name = nameField.Text.Trim(),
            SongTitle = titleField.Text.Trim(),
            Artist = artistField.Text.Trim(),
            RequestedStars = starsSlider.Value,
            Tags = tagsField.Text.Trim(),
            Description = descriptionField.Text.Trim(),
            BackgroundVideoUrl = videoField.Text.Trim(),
            Audio = new NewgroundsAudio(selectedId, importedAudio?.Sha256 ?? expectedAudioHash)
        };
    }

    private void UpdateValidation()
    {
        BeatmapDraft draft = BuildDraft();
        pageButton.Enabled = selectedId > 0;
        importButton.Enabled = selectedId > 0 && importTask == null;
        importButton.Label = importedAudio == null ? "Choose audio file" : "Replace audio file";
        previewButton.Enabled = hasMusic && importTask == null;
        copyButton.Enabled = NewgroundsAudio.IsValidHash(importedAudio?.Sha256 ?? expectedAudioHash);
        nameField.Invalid = showValidation && string.IsNullOrWhiteSpace(draft.Name);
        titleField.Invalid = showValidation && string.IsNullOrWhiteSpace(draft.SongTitle);
        artistField.Invalid = showValidation && string.IsNullOrWhiteSpace(draft.Artist);
        idField.Invalid = (showValidation || idField.Text.Length > 0) && selectedId <= 0;
        videoField.Invalid = videoField.Text.Length > 0 && !ValidVideoUrl();
    }

    private bool ValidVideoUrl()
    {
        return Uri.TryCreate(videoField.Text.Trim(), UriKind.Absolute, out Uri? url) &&
            (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp);
    }

    private bool SaveDraft()
    {
        showValidation = true;
        BeatmapDraft draft = BuildDraft();
        string? error = draft.Validate();
        if (error != null || (importTask != null && !restoringAudio) ||
            (importedAudio == null && !NewgroundsAudio.IsValidHash(expectedAudioHash)))
        {
            footerMessage = importTask != null ? "Wait for the audio import to finish." : error ?? "Import the audio before saving.";
            footerError = true;
            UpdateValidation();
            return false;
        }

        if (IsSaved())
        {
            return true;
        }

        try
        {
            savedPath = draft.Save(Path.Combine(storageRoot, "Drafts"), savedPath);
            savedDraft = draft;
            expectedAudioHash = draft.Audio.Sha256;
            savedInput = InputSnapshot();
            footerMessage = "Changes saved.";
            footerError = false;
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            footerMessage = "Could not save. Check storage permissions and try again.";
            footerError = true;
            return false;
        }
    }

    private string InputSnapshot()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Fields = fields.Select(field => field.Text),
            RequestedStars = starsSlider.Value
        });
    }

    private bool IsSaved()
    {
        return savedDraft != null && savedPath != null && File.Exists(savedPath) &&
            (importTask == null || restoringAudio) && BuildDraft() == savedDraft && InputSnapshot() == savedInput;
    }

    private void RequestExit()
    {
        if (IsSaved())
        {
            ReturnToMenu();
            return;
        }

        exitPopupOpen = true;
        popupError = "";
        keyboardFocus = -1;
        ApplyFocus();
        foreach (MenuButton button in exitButtons)
        {
            button.ResetInteraction();
        }
    }

    private void ReturnToMenu()
    {
        game.ChangeScreen(new MainMenu(game, startOpen: true));
    }

    private void CloseExitPopup()
    {
        exitPopupOpen = false;
        popupError = "";
        foreach (MenuButton button in exitButtons)
        {
            button.ResetInteraction();
        }
    }

    private void SaveAndReturn()
    {
        if (SaveDraft())
        {
            ReturnToMenu();
            return;
        }

        popupError = footerMessage;
    }

    private void UpdateExitPopup(float deltaTime)
    {
        saveIconRetry += deltaTime;
        if (saveIcon.Id == 0 && saveIconRetry >= 1f)
        {
            saveIconRetry = 0f;
            TryLoadSaveIcon();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            CloseExitPopup();
            return;
        }

        for (int i = 0; i < exitButtons.Length; i++)
        {
            exitButtons[i].Update(deltaTime, exitPopupOpen);
            if (!exitPopupOpen)
            {
                break;
            }
        }

        if (Raylib.IsFileDropped())
        {
            Raylib.GetDroppedFiles();
        }
    }

    private void TryLoadSaveIcon()
    {
        if (saveIcon.Id != 0)
        {
            return;
        }

        string path = MainMenu.AssetPath("editor/save.png");
        if (!File.Exists(path))
        {
            return;
        }

        Texture2D loaded = Raylib.LoadTexture(path);
        if (!Raylib.IsTextureValid(loaded))
        {
            return;
        }

        saveIcon = loaded;
        Raylib.SetTextureFilter(saveIcon, TextureFilter.Bilinear);
    }

    private void DrawExitPopup()
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(8, 7, 18, 200));
        Color background = new(38, 34, 70, 255);
        Raylib.DrawRectangleRounded(popupBounds, EditorTheme.Roundness(popupBounds, 10f * scale), 16, background);
        Raylib.DrawRectangleRoundedLinesEx(popupBounds, EditorTheme.Roundness(popupBounds, 10f * scale),
            16, scale, new Color(87, 77, 132, 255));
        if (saveIcon.Id != 0)
        {
            float size = 64f * scale;
            Rectangle source = new(0, 0, saveIcon.Width, saveIcon.Height);
            Rectangle destination = new(popupBounds.X + (popupBounds.Width - size) / 2f,
                popupBounds.Y + 26f * scale, size, size);
            Raylib.DrawTexturePro(saveIcon, source, destination, Vector2.Zero, 0f, Color.White);
        }

        DrawCentered("Save your changes?", new Rectangle(popupBounds.X, popupBounds.Y + 104f * scale,
            popupBounds.Width, 44f * scale), 32f * scale, Color.White);
        if (popupError.Length > 0)
        {
            DrawWrapped(popupError, new Vector2(popupBounds.X + 48f * scale, popupBounds.Y + 159f * scale),
                popupBounds.Width - 96f * scale, 14f * scale, EditorTheme.Error, 2);
        }

        foreach (MenuButton button in exitButtons)
        {
            button.Draw(1f);
        }

    }

    public void Draw()
    {
        Raylib.ClearBackground(EditorTheme.Background);
        AppFont.Draw("Level setup", new Vector2(detailsPanel.X, 44f * scale), 34f * scale, Color.White);
        DrawPanel(detailsPanel, "Beatmap details");
        DrawPanel(audioPanel, "Music source");
        DrawMetadata();
        DrawAudio();
        if (footerMessage.Length > 0)
        {
            DrawWrapped(footerMessage, new Vector2(detailsPanel.X, Raylib.GetScreenHeight() - 40f * scale),
                audioPanel.X + audioPanel.Width - detailsPanel.X, 14f * scale,
                footerError ? EditorTheme.Error : EditorTheme.SecondaryText, 2);
        }

        if (exitPopupOpen)
        {
            DrawExitPopup();
        }
    }

    private void DrawPanel(Rectangle panel, string title)
    {
        Raylib.DrawRectangleRounded(panel, EditorTheme.Roundness(panel, 8f * scale), 12, EditorTheme.Surface);
        AppFont.Draw(title, new Vector2(panel.X + 24f * scale, panel.Y + 23f * scale), 20f * scale, Color.White);
    }

    private void DrawMetadata()
    {
        DrawField("Level name *", nameField);
        DrawField("Song title *", titleField);
        DrawField("Artist *", artistField);
        AppFont.Draw("Stars Requested", new Vector2(starsSlider.Bounds.X, starsSlider.Bounds.Y - 24f * scale),
            15f * scale, Color.White);
        starsSlider.Draw(scale);
        DrawField("Tags", tagsField);
        DrawField("Description", descriptionField);
        DrawField("Background video URL (optional)", videoField);
        if (videoField.Invalid)
        {
            DrawWrapped("Enter a complete http:// or https:// URL.",
                new Vector2(videoField.Bounds.X, videoField.Bounds.Y + videoField.Bounds.Height + 12f * scale),
                videoField.Bounds.Width, 13f * scale, EditorTheme.Error, 2);
        }
    }

    private void DrawField(string label, TextField field)
    {
        AppFont.Draw(label, new Vector2(field.Bounds.X, field.Bounds.Y - 24f * scale), 15f * scale, Color.White);
        field.Draw();
    }

    private void DrawAudio()
    {
        DrawField("Newgrounds audio ID *", idField);
        pageButton.Draw(scale);
        float x = idField.Bounds.X;
        float width = idField.Bounds.Width;

        bool dropHighlighted = dropHovered || Raylib.IsWindowFocused() &&
            Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), importButton.Bounds);
        Raylib.DrawRectangleRoundedLinesEx(dropArea, EditorTheme.Roundness(dropArea, 6f * scale), 12,
            dropHighlighted ? 2f * scale : scale, dropHighlighted ? EditorTheme.Accent : EditorTheme.Border);
        importButton.Draw(scale);
        string dropText = importTask != null ? "Calculating SHA-256..." : "or drop an MP3, OGG, or WAV";
        DrawCentered(dropText, new Rectangle(dropArea.X, dropArea.Y + 56f * scale, dropArea.Width, 20f * scale),
            13f * scale, EditorTheme.SecondaryText);

        float statusY = audioPanel.Y + 334f * scale;
        if (importError.Length > 0)
        {
            DrawWrapped(importError, new Vector2(x, statusY), width, 14f * scale, EditorTheme.Error, 3);
        }
        else if (importedAudio == null)
        {
            AppFont.Draw(importTask == null ? "No audio imported" : "Reading the original audio file",
                new Vector2(x, statusY), 16f * scale, Color.White);
        }
        else
        {
            AppFont.Draw("Audio ready", new Vector2(x, statusY), 16f * scale, EditorTheme.Success);
            DrawEllipsis(importedAudio.FileName, new Vector2(x, statusY + 24f * scale), width, 15f * scale, Color.White);
            string info = $"{(int)duration / 60}:{(int)duration % 60:00} | {importedAudio.SizeBytes / 1048576f:0.00} MB";
            AppFont.Draw(info, new Vector2(x, statusY + 46f * scale), 13f * scale, EditorTheme.SecondaryText);
        }

        AppFont.Draw("SHA-256", new Vector2(x, audioPanel.Y + 410f * scale),
            13f * scale, EditorTheme.SecondaryText);
        string hash = importedAudio?.Sha256 ?? expectedAudioHash;
        if (!NewgroundsAudio.IsValidHash(hash))
        {
            AppFont.Draw("Calculated after import", new Vector2(x, audioPanel.Y + 439f * scale),
                14f * scale, EditorTheme.Placeholder);
        }
        else
        {
            AppFont.Draw(hash[..32], new Vector2(x, audioPanel.Y + 436f * scale), 14f * scale, Color.White);
            AppFont.Draw(hash[32..], new Vector2(x, audioPanel.Y + 455f * scale), 14f * scale, Color.White);
        }

        previewButton.Draw(scale);
        copyButton.Draw(scale);
    }

    private static void DrawCentered(string text, Rectangle bounds, float size, Color color)
    {
        Vector2 measured = AppFont.Measure(text, size);
        AppFont.Draw(text, new Vector2(bounds.X + (bounds.Width - measured.X) / 2f,
            bounds.Y + (bounds.Height - measured.Y) / 2f), size, color);
    }

    private static void DrawEllipsis(string text, Vector2 position, float width, float size, Color color)
    {
        if (AppFont.Measure(text, size).X <= width)
        {
            AppFont.Draw(text, position, size, color);
            return;
        }

        while (text.Length > 0 && AppFont.Measure(text + "...", size).X > width)
        {
            text = text[..^1];
        }

        AppFont.Draw(text + "...", position, size, color);
    }

    private static void DrawWrapped(string text, Vector2 position, float width, float size, Color color, int maxLines)
    {
        string line = "";
        int row = 0;
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && AppFont.Measure(candidate, size).X > width)
            {
                AppFont.Draw(line, position + new Vector2(0f, row * size * 1.4f), size, color);
                row++;
                if (row >= maxLines)
                {
                    return;
                }

                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        DrawEllipsis(line, position + new Vector2(0f, row * size * 1.4f), width, size, color);
    }
}
