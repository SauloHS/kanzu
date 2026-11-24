using Raylib_cs;

namespace Kanzu;

public static class Sfx
{
    public static Sound Click { get; private set; } = default!;
    public static Sound Hover { get; private set; } = default!;

    public static void Load()
    {
        Click = Raylib.LoadSound(MainMenu.AssetPath("sfx/click.ogg"));
        Hover = Raylib.LoadSound(MainMenu.AssetPath("sfx/hover.ogg"));
    }

    public static void Unload()
    {
        Raylib.UnloadSound(Click);
        Raylib.UnloadSound(Hover);
    }

    public static void PlayClick()
    {
        Raylib.PlaySound(Click);
    }

    public static void PlayHover()
    {
        Raylib.PlaySound(Hover);
    }
}
