using Raylib_cs;
using System.Numerics;

namespace Kanzu;

public class EditorScreen : IScreen
{
    private readonly Game game;

    public EditorScreen(Game game)
    {
        this.game = game;
    }

    public void Load() { }
    public void Unload() { }
    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            game.ChangeScreen(new MainMenu(game, startOpen: true));
        }
    }

    public void Draw()
    {
        Raylib.ClearBackground(new Color(43, 39, 64, 255));
        AppFont.Draw("Editor", new Vector2(40, 40), 48, Color.White);
    }
}
