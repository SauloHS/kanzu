using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public class CustomCursor
{
    private const float Size = 32f;
    private static readonly Vector2 Hotspot = new(0f, 0f);

    private Texture2D texture;

    public void Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "cursor", "normal.png");
        texture = Raylib.LoadTexture(path);
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        Raylib.HideCursor();
    }

    public void Unload()
    {
        Raylib.UnloadTexture(texture);
    }

    public void Draw()
    {
        Vector2 mouse = Raylib.GetMousePosition();
        float width = Size * texture.Width / texture.Height;

        Rectangle source = new(0, 0, texture.Width, texture.Height);
        Rectangle destination = new(mouse.X, mouse.Y, width, Size);
        Raylib.DrawTexturePro(texture, source, destination, Hotspot, 0f, Color.White);
    }
}
