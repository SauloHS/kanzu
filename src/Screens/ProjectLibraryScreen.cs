using System.Globalization;
using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public sealed class ProjectLibraryScreen : IScreen
{
    private readonly Game game;
    private readonly EditorButton createButton = new("Create new", primary: true);
    private readonly EditorButton cancelDeleteButton = new("Cancel");
    private readonly EditorButton confirmDeleteButton = new("Delete") { Destructive = true };
    private readonly string draftsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kanzu", "Drafts");
    private List<SavedProject> projects = [];
    private List<EditorButton> rows = [];
    private List<EditorButton> deleteButtons = [];
    private SavedProject? pendingDeletion;
    private Rectangle deletePopup;
    private string deleteError = "";
    private Rectangle listBounds;
    private float scale;
    private float scroll;
    private string error = "";

    public ProjectLibraryScreen(Game game)
    {
        this.game = game;
    }

    public void Load()
    {
        RefreshProjects();
        Layout();
    }

    private void RefreshProjects()
    {
        error = "";
        try
        {
            projects = ProjectStore.Load(draftsDirectory, out int unreadable);
            rows = projects.Select(project => new EditorButton(project.Draft.Name)).ToList();
            deleteButtons = projects.Select(_ => new EditorButton("Delete") { Destructive = true }).ToList();
            if (unreadable > 0)
            {
                error = $"{unreadable} saved {(unreadable == 1 ? "project could" : "projects could")} not be read.";
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            error = "Could not read saved projects. Check folder permissions and reopen the editor.";
        }

        Layout();
    }

    public void Unload()
    {
    }

    public void Update()
    {
        Layout();
        if (pendingDeletion != null)
        {
            UpdateDeletePopup();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            game.ChangeScreen(new MainMenu(game, startOpen: true));
            return;
        }

        if (createButton.Update())
        {
            game.ChangeScreen(new EditorScreen(game));
            return;
        }

        bool insideList = Raylib.IsWindowFocused() &&
            Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), listBounds);
        if (insideList)
        {
            scroll = Math.Clamp(scroll - Raylib.GetMouseWheelMove() * 54f * scale, 0f, MaxScroll());
            LayoutRows();
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool visible = Raylib.CheckCollisionRecs(rows[i].Bounds, listBounds);
            bool interactive = insideList && visible;
            bool overDelete = interactive && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), deleteButtons[i].Bounds);
            if (deleteButtons[i].Update(interactive))
            {
                RequestDeletion(projects[i]);
                return;
            }

            if (rows[i].Update(interactive && !overDelete))
            {
                game.ChangeScreen(new EditorScreen(game, projects[i]));
                return;
            }
        }
    }

    private void RequestDeletion(SavedProject project)
    {
        pendingDeletion = project;
        deleteError = "";
    }

    private void UpdateDeletePopup()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || cancelDeleteButton.Update())
        {
            pendingDeletion = null;
            return;
        }

        if (confirmDeleteButton.Update())
        {
            DeleteSelectedProject();
        }
    }

    private void DeleteSelectedProject()
    {
        if (pendingDeletion == null)
        {
            return;
        }

        try
        {
            ProjectStore.Delete(draftsDirectory, pendingDeletion.Path);
            pendingDeletion = null;
            RefreshProjects();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            deleteError = "Could not delete this map. Check file permissions and try again.";
        }
    }

    private void DrawDeletePopup()
    {
        if (pendingDeletion == null)
        {
            return;
        }

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(8, 7, 18, 200));
        Raylib.DrawRectangleRounded(deletePopup, EditorTheme.Roundness(deletePopup, 10f * scale),
            16, new Color(38, 34, 70, 255));
        Raylib.DrawRectangleRoundedLinesEx(deletePopup, EditorTheme.Roundness(deletePopup, 10f * scale),
            16, scale, new Color(87, 77, 132, 255));
        float left = deletePopup.X + 32f * scale;
        float width = deletePopup.Width - 64f * scale;
        AppFont.Draw("Delete this map?", new Vector2(left, deletePopup.Y + 30f * scale), 28f * scale, Color.White);
        DrawEllipsis(pendingDeletion.Draft.Name, new Vector2(left, deletePopup.Y + 86f * scale),
            width, 21f * scale, Color.White);
        AppFont.Draw("This cannot be undone.", new Vector2(left, deletePopup.Y + 125f * scale),
            16f * scale, EditorTheme.SecondaryText);
        if (deleteError.Length > 0)
        {
            DrawEllipsis(deleteError, new Vector2(left, deletePopup.Y + 163f * scale),
                width, 13f * scale, EditorTheme.Error);
        }

        cancelDeleteButton.Draw(scale);
        confirmDeleteButton.Draw(scale);
    }

    private void Layout()
    {
        float width = Raylib.GetScreenWidth();
        float height = Raylib.GetScreenHeight();
        scale = MathF.Min(width / 1280f, height / 780f);
        float contentWidth = MathF.Min(width - 80f * scale, 1180f * scale);
        float left = (width - contentWidth) / 2f;
        createButton.Bounds = new Rectangle(left + contentWidth - 172f * scale,
            46f * scale, 172f * scale, 44f * scale);
        listBounds = new Rectangle(left, 140f * scale, contentWidth, height - 196f * scale);
        float popupWidth = 560f * scale;
        float popupHeight = 288f * scale;
        deletePopup = new Rectangle((width - popupWidth) / 2f, (height - popupHeight) / 2f, popupWidth, popupHeight);
        float buttonWidth = (popupWidth - 80f * scale) / 2f;
        cancelDeleteButton.Bounds = new Rectangle(deletePopup.X + 32f * scale,
            deletePopup.Y + popupHeight - 72f * scale, buttonWidth, 40f * scale);
        confirmDeleteButton.Bounds = new Rectangle(cancelDeleteButton.Bounds.X + buttonWidth + 16f * scale,
            cancelDeleteButton.Bounds.Y, buttonWidth, 40f * scale);
        scroll = Math.Clamp(scroll, 0f, MaxScroll());
        LayoutRows();
    }

    private float MaxScroll()
    {
        float contentHeight = projects.Count * 108f * scale - 12f * scale;
        return MathF.Max(0f, contentHeight - listBounds.Height);
    }

    private void LayoutRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Bounds = new Rectangle(listBounds.X + scale, listBounds.Y + i * 108f * scale - scroll,
                listBounds.Width - 14f * scale, 96f * scale);
            Rectangle row = rows[i].Bounds;
            deleteButtons[i].Bounds = new Rectangle(row.X + row.Width - 112f * scale,
                row.Y + 30f * scale, 88f * scale, 36f * scale);
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(EditorTheme.Background);
        AppFont.Draw("Your projects", new Vector2(listBounds.X, 44f * scale), 34f * scale, Color.White);
        string count = $"{projects.Count} {(projects.Count == 1 ? "project" : "projects")}";
        AppFont.Draw(count, new Vector2(listBounds.X, 94f * scale), 15f * scale, EditorTheme.SecondaryText);
        createButton.Draw(scale);

        if (projects.Count == 0)
        {
            string text = "No saved projects";
            float size = 22f * scale;
            Vector2 measured = AppFont.Measure(text, size);
            AppFont.Draw(text, new Vector2(listBounds.X + (listBounds.Width - measured.X) / 2f,
                listBounds.Y + listBounds.Height * 0.4f), size, EditorTheme.SecondaryText);
        }
        else
        {
            Raylib.BeginScissorMode((int)listBounds.X, (int)listBounds.Y,
                (int)listBounds.Width, (int)listBounds.Height);
            for (int i = 0; i < projects.Count; i++)
            {
                if (Raylib.CheckCollisionRecs(rows[i].Bounds, listBounds))
                {
                    DrawProject(i);
                }
            }

            Raylib.EndScissorMode();
            DrawScrollbar();
        }

        if (error.Length > 0)
        {
            AppFont.Draw(error, new Vector2(listBounds.X, Raylib.GetScreenHeight() - 36f * scale),
                14f * scale, EditorTheme.Error);
        }

        DrawDeletePopup();
    }

    private void DrawProject(int index)
    {
        SavedProject project = projects[index];
        Rectangle area = rows[index].Bounds;
        rows[index].Draw(scale, showLabel: false);
        float left = area.X + 24f * scale;
        float textWidth = area.Width - 392f * scale;
        DrawEllipsis(project.Draft.Name, new Vector2(left, area.Y + 18f * scale),
            textWidth, 23f * scale, Color.White);
        DrawEllipsis(project.Draft.SongTitle + " - " + project.Draft.Artist,
            new Vector2(left, area.Y + 54f * scale), textWidth, 15f * scale, EditorTheme.SecondaryText);

        string stars = $"{project.Draft.RequestedStars} stars requested";
        Vector2 starsSize = AppFont.Measure(stars, 16f * scale);
        AppFont.Draw(stars, new Vector2(area.X + area.Width - 136f * scale - starsSize.X,
            area.Y + 24f * scale), 16f * scale, EditorTheme.Accent);
        string updated = project.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        Vector2 dateSize = AppFont.Measure(updated, 13f * scale);
        AppFont.Draw(updated, new Vector2(area.X + area.Width - 136f * scale - dateSize.X,
            area.Y + 57f * scale), 13f * scale, EditorTheme.SecondaryText);
        deleteButtons[index].Draw(scale);
    }

    private void DrawScrollbar()
    {
        float maxScroll = MaxScroll();
        if (maxScroll <= 0f)
        {
            return;
        }

        float contentHeight = listBounds.Height + maxScroll;
        float thumbHeight = MathF.Max(28f * scale, listBounds.Height * listBounds.Height / contentHeight);
        float y = listBounds.Y + (listBounds.Height - thumbHeight) * scroll / maxScroll;
        Raylib.DrawRectangleRec(new Rectangle(listBounds.X + listBounds.Width - 5f * scale,
            y, 3f * scale, thumbHeight), EditorTheme.Accent);
    }

    private static void DrawEllipsis(string text, Vector2 position, float width, float size, Color color)
    {
        string shown = text;
        if (AppFont.Measure(shown, size).X > width)
        {
            while (shown.Length > 0 && AppFont.Measure(shown + "...", size).X > width)
            {
                shown = shown[..^1];
            }

            shown += "...";
        }

        AppFont.Draw(shown, position, size, color);
    }
}
