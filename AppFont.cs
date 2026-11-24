using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public static class AppFont
{
    private const float Spacing = 1f;

    public static Font Current { get; private set; }

    public static void Load()
    {
        int[] codepoints = Enumerable.Range(32, 224).ToArray();
        Current = Raylib.LoadFontEx(MainMenu.AssetPath("fonts/ui.ttf"), 64, codepoints, codepoints.Length);
        Raylib.SetTextureFilter(Current.Texture, TextureFilter.Bilinear);
    }

    public static bool Supports(int codepoint)
    {
        return (codepoint >= 32 && codepoint <= 126) || (codepoint >= 160 && codepoint <= 255);
    }
    public static void Unload()
    {
        Raylib.UnloadFont(Current);
    }

    public static Vector2 Measure(string text, float size)
    {
        return Raylib.MeasureTextEx(Current, text, size, Spacing);
    }

    public static void Draw(string text, Vector2 position, float size, Color color)
    {
        Raylib.DrawTextEx(Current, text, position, size, Spacing, color);
    }
}
