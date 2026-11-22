using Raylib_cs;

namespace Kanzu;

public class Game
{
    private const int Width = 1280;
    private const int Height = 720;
    private readonly MainMenu mainMenu = new();
    private readonly CustomCursor cursor = new();

    public void Run()
    {
        Raylib.InitWindow(Width, Height, "Kanzu");
        Raylib.ToggleBorderlessWindowed();
        Raylib.SetWindowFocused();
        LoadWindowIcon();
        Raylib.InitAudioDevice();
        mainMenu.Load();
        cursor.Load();

        while (!Raylib.WindowShouldClose())
        {
            Update();
            Draw();
        }
        mainMenu.Unload();
        cursor.Unload();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    private void Update()
    {
        mainMenu.Update();
    }

    private void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(245, 238, 220, 255));
        mainMenu.Draw();
        cursor.Draw();
        Raylib.EndDrawing();
    }

    private void LoadWindowIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        Image icon = Raylib.LoadImage(path);
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);
    }
}
