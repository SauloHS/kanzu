using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public sealed class EditorButton
{
    private bool wasHovering;

    public string Label { get; set; }
    public Rectangle Bounds { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Focused { get; set; }
    public bool Primary { get; set; }
    public bool Destructive { get; set; }

    public EditorButton(string label, bool primary = false)
    {
        Label = label;
        Primary = primary;
    }

    public bool Update(bool interactive = true)
    {
        bool available = interactive && Raylib.IsWindowFocused();
        bool hovering = available && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
        if (hovering && !wasHovering)
        {
            Sfx.PlayHover();
        }

        wasHovering = hovering;
        bool pressed = hovering && Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool keyboard = available && Focused && (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space));
        if (!Enabled || (!pressed && !keyboard))
        {
            return false;
        }

        Sfx.PlayClick();
        return true;
    }

    public void Draw(float scale, bool showLabel = true)
    {
        bool hovering = wasHovering && Raylib.IsWindowFocused();
        bool highlighted = hovering || Focused;
        Color fill = Destructive ? new Color(173, 55, 78, 255) : Primary ? EditorTheme.Primary : EditorTheme.Field;
        if (hovering)
        {
            fill = Destructive ? new Color(192, 65, 90, 255) : Primary ? EditorTheme.PrimaryHover : EditorTheme.FieldFocus;
        }

        if (!Enabled)
        {
            fill = EditorTheme.Surface;
        }

        float roundness = EditorTheme.Roundness(Bounds, 6f * scale);
        Raylib.DrawRectangleRounded(Bounds, roundness, 12, fill);
        Raylib.DrawRectangleRoundedLinesEx(Bounds, roundness, 12, highlighted ? 2f * scale : scale,
            highlighted ? EditorTheme.Accent : EditorTheme.Border);
        if (!showLabel)
        {
            return;
        }

        float size = 16f * scale;
        Vector2 textSize = AppFont.Measure(Label, size);
        Vector2 position = new(Bounds.X + (Bounds.Width - textSize.X) / 2f,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2f);
        AppFont.Draw(Label, position, size, Enabled ? Color.White : EditorTheme.Placeholder);
    }
}
