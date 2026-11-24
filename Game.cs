using Raylib_cs;

namespace Kanzu;

public class Game
{
    private const int Width = 1280;
    private const int Height = 720;
    private IScreen currentScreen = null!;
    private IScreen? nextScreen;
    private bool exitRequested;
    private readonly CustomCursor cursor = new();

    public void Run()
    {
        Raylib.InitWindow(Width, Height, "Kanzu");
        AppFont.Load();
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.ToggleBorderlessWindowed();
        Raylib.ClearWindowState(ConfigFlags.TopmostWindow);
        Raylib.SetWindowFocused();
        LoadWindowIcon();
        Raylib.InitAudioDevice();
        Sfx.Load();
        currentScreen = new MainMenu(this);
        currentScreen.Load();
        cursor.Load();

        while (!Raylib.WindowShouldClose() && !exitRequested)
        {
            Update();
            Draw();
        }
        currentScreen.Unload();
        cursor.Unload();
        AppFont.Unload();
        Sfx.Unload();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    private void Update()
    {
        ApplyScreenChange();
        currentScreen.Update();
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(245, 238, 220, 255));
        currentScreen.Draw();
        cursor.Draw();
        Raylib.EndDrawing();
    }

    public void ChangeScreen(IScreen screen)
    {
        nextScreen = screen;
    }

    public void Exit()
    {
        exitRequested = true;
    }

    private void ApplyScreenChange()
    {
        if (nextScreen == null)
        {
            return;
        }

        currentScreen.Unload();
        currentScreen = nextScreen;
        currentScreen.Load();
        nextScreen = null;
    }

    private void LoadWindowIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        Image icon = Raylib.LoadImage(path);
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);
    }
}
