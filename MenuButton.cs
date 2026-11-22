using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public class MenuButton
{
    public const float SlantRatio = 0.27f;

    private const float HoverGrow = 0.06f;
    private const float HoverSpeed = 14f;
    private const float FontRatio = 0.22f;
    private const float IconRatio = 0.45f;
    private const float ContentGapRatio = 0.06f;
    private const float Spacing = 1f;

    private readonly string label;
    private readonly string iconPath;
    private readonly Color color;
    private readonly Action? onClick;
    private Texture2D icon;
    private float hoverBlend;

    public Rectangle Bounds { get; set; }
    public float HiddenRight { get; set; }

    public MenuButton(string label, string iconPath, Color color, Action? onClick)
    {
        this.label = label;
        this.iconPath = iconPath;
        this.color = color;
        this.onClick = onClick;
    }

    public void Load()
    {
        icon = Raylib.LoadTexture(MainMenu.AssetPath(iconPath));
        Raylib.SetTextureFilter(icon, TextureFilter.Bilinear);
    }

    public void Unload()
    {
        Raylib.UnloadTexture(icon);
    }

    public void Update(float deltaTime, bool interactive)
    {
        bool hovering = interactive && Contains(Raylib.GetMousePosition());
        float target = hovering ? 1f : 0f;
        hoverBlend += (target - hoverBlend) * MathF.Min(1f, HoverSpeed * deltaTime);

        if (hovering && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            onClick?.Invoke();
        }
    }

    private bool Contains(Vector2 point)
    {
        float slant = Bounds.Height * SlantRatio;
        float bottom = Bounds.Y + Bounds.Height;

        if (point.Y < Bounds.Y || point.Y > bottom)
        {
            return false;
        }

        float left = Bounds.X + slant * (bottom - point.Y) / Bounds.Height;
        float right = left + Bounds.Width - slant;
        return point.X >= left && point.X <= right;
    }

    public void Draw(Font font, float alpha)
    {
        Rectangle area = Scaled(Bounds, 1f + HoverGrow * hoverBlend);
        float slant = area.Height * SlantRatio;
        byte fade = (byte)(255 * alpha);
        Color fill = new(color.R, color.G, color.B, fade);
        Color white = new((byte)255, (byte)255, (byte)255, fade);

        Vector2 topLeft = new(area.X + slant, area.Y);
        Vector2 topRight = new(area.X + area.Width, area.Y);
        Vector2 bottomLeft = new(area.X, area.Y + area.Height);
        Vector2 bottomRight = new(area.X + area.Width - slant, area.Y + area.Height);

        Raylib.DrawTriangle(topLeft, bottomLeft, bottomRight, fill);
        Raylib.DrawTriangle(topLeft, bottomRight, topRight, fill);

        float fontSize = area.Height * FontRatio;
        float iconSize = area.Height * IconRatio;
        float gap = area.Height * ContentGapRatio;
        Vector2 textSize = Raylib.MeasureTextEx(font, label, fontSize, Spacing);

        float groupHeight = iconSize + gap + textSize.Y;
        float top = area.Y + (area.Height - groupHeight) / 2f;
        float centerX = area.X + (area.Width - HiddenRight) / 2f;

        Rectangle iconSource = new(0, 0, icon.Width, icon.Height);
        Rectangle iconTarget = new(centerX - iconSize / 2f, top, iconSize, iconSize);
        Raylib.DrawTexturePro(icon, iconSource, iconTarget, Vector2.Zero, 0f, white);

        Vector2 textPosition = new(centerX - textSize.X / 2f, top + iconSize + gap);
        Raylib.DrawTextEx(font, label, textPosition, fontSize, Spacing, white);
    }

    private static Rectangle Scaled(Rectangle rectangle, float scale)
    {
        float width = rectangle.Width * scale;
        float height = rectangle.Height * scale;
        float x = rectangle.X - (width - rectangle.Width) / 2f;
        float y = rectangle.Y - (height - rectangle.Height) / 2f;
        return new Rectangle(x, y, width, height);
    }
}
