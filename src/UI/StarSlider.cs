using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public sealed class StarSlider
{
    private int value = 1;
    private bool dragging;
    private bool wasHovering;

    public Rectangle Bounds { get; set; }
    public bool Focused { get; set; }
    public int Value
    {
        get => value;
        set => this.value = Math.Clamp(value, 1, 10);
    }

    public static int ValueAt(float position)
    {
        return 1 + (int)MathF.Round(Math.Clamp(position, 0f, 1f) * 9f, MidpointRounding.AwayFromZero);
    }

    public void Update()
    {
        bool windowFocused = Raylib.IsWindowFocused();
        bool hovering = windowFocused && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
        if (hovering && !wasHovering)
        {
            Sfx.PlayHover();
        }

        wasHovering = hovering;
        if (!windowFocused)
        {
            dragging = false;
            return;
        }

        if (hovering && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            dragging = true;
            Sfx.PlayClick();
        }

        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            dragging = false;
        }

        if (dragging)
        {
            float scale = Bounds.Height / 40f;
            float left = Bounds.X + 16f * scale;
            float width = MathF.Max(1f, Bounds.Width - 72f * scale);
            Value = ValueAt((Raylib.GetMousePosition().X - left) / width);
        }

        if (!Focused)
        {
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressedRepeat(KeyboardKey.Left))
        {
            Value--;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressedRepeat(KeyboardKey.Right))
        {
            Value++;
        }
    }

    public void Draw(float scale)
    {
        bool hovering = Raylib.IsWindowFocused() && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
        bool highlighted = hovering || Focused || dragging;
        float roundness = EditorTheme.Roundness(Bounds, 6f * scale);
        Raylib.DrawRectangleRounded(Bounds, roundness, 12, highlighted ? EditorTheme.FieldFocus : EditorTheme.Field);
        Raylib.DrawRectangleRoundedLinesEx(Bounds, roundness, 12, highlighted ? 2f * scale : scale,
            highlighted ? EditorTheme.Accent : EditorTheme.Border);

        float left = Bounds.X + 16f * scale;
        float width = MathF.Max(1f, Bounds.Width - 72f * scale);
        float y = Bounds.Y + Bounds.Height / 2f;
        float knobX = left + width * (Value - 1f) / 9f;
        Raylib.DrawLineEx(new Vector2(left, y), new Vector2(left + width, y), 4f * scale, EditorTheme.Border);
        Raylib.DrawLineEx(new Vector2(left, y), new Vector2(knobX, y), 4f * scale, EditorTheme.Accent);
        for (int i = 0; i < 10; i++)
        {
            Raylib.DrawCircleV(new Vector2(left + width * i / 9f, y), 2f * scale,
                i < Value ? EditorTheme.Accent : EditorTheme.Placeholder);
        }

        Raylib.DrawCircleV(new Vector2(knobX, y), 7f * scale, Color.White);
        string label = Value.ToString();
        Vector2 textSize = AppFont.Measure(label, 18f * scale);
        AppFont.Draw(label, new Vector2(Bounds.X + Bounds.Width - 24f * scale - textSize.X / 2f,
            Bounds.Y + (Bounds.Height - textSize.Y) / 2f), 18f * scale, Color.White);
    }
}
