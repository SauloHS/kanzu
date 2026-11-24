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

    private readonly string label;
    private readonly string iconPath;
    private readonly Color color;
    private readonly Action? onClick;
    private Texture2D icon;
    private float hoverBlend;
    private bool wasHovering;
    private float iconRetry;
    private readonly HoldConfirmation hold = new();

    public Rectangle Bounds { get; set; }
    public float HiddenRight { get; set; }
    public float HoldSeconds { get; set; }
    public float HoldProgress => hold.VisualProgress;

    public MenuButton(string label, string iconPath, Color color, Action? onClick)
    {
        this.label = label;
        this.iconPath = iconPath;
        this.color = color;
        this.onClick = onClick;
    }

    public void Load()
    {
        TryLoadIcon();
    }

    private void TryLoadIcon()
    {
        if (icon.Id != 0 || string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        string path = MainMenu.AssetPath(iconPath);
        if (!File.Exists(path))
        {
            return;
        }

        Texture2D loaded = Raylib.LoadTexture(path);
        if (!Raylib.IsTextureValid(loaded))
        {
            return;
        }

        icon = loaded;
        Raylib.SetTextureFilter(icon, TextureFilter.Bilinear);
    }

    public void Unload()
    {
        if (icon.Id != 0)
        {
            Raylib.UnloadTexture(icon);
            icon = default;
        }
    }

    public void ResetInteraction()
    {
        hold.Reset();
        hoverBlend = 0f;
        wasHovering = false;
    }

    public void Update(float deltaTime, bool interactive)
    {
        iconRetry += deltaTime;
        if (icon.Id == 0 && iconRetry >= 1f)
        {
            iconRetry = 0f;
            TryLoadIcon();
        }

        interactive &= Raylib.IsWindowFocused();
        bool hovering = interactive && Contains(Raylib.GetMousePosition());
        if (hovering && !wasHovering)
        {
            Sfx.PlayHover();
        }

        wasHovering = hovering;
        float target = hovering ? 1f : 0f;
        hoverBlend += (target - hoverBlend) * MathF.Min(1f, HoverSpeed * deltaTime);

        bool mousePressed = hovering && Raylib.IsMouseButtonPressed(MouseButton.Left);
        if (HoldSeconds > 0f)
        {
            bool held = hovering && Raylib.IsMouseButtonDown(MouseButton.Left);
            if (!hold.Update(deltaTime, mousePressed, held, HoldSeconds))
            {
                return;
            }
        }
        else if (!mousePressed)
        {
            return;
        }

        Sfx.PlayClick();
        onClick?.Invoke();
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

    public void Draw(float alpha)
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
        if (wasHovering)
        {
            float thickness = area.Height * 0.025f;
            Raylib.DrawLineEx(topLeft, topRight, thickness, white);
            Raylib.DrawLineEx(topRight, bottomRight, thickness, white);
            Raylib.DrawLineEx(bottomRight, bottomLeft, thickness, white);
            Raylib.DrawLineEx(bottomLeft, topLeft, thickness, white);
        }

        float fontSize = area.Height * FontRatio;
        bool hasIcon = icon.Id != 0;
        float iconSize = hasIcon ? area.Height * IconRatio : 0f;
        float gap = hasIcon ? area.Height * ContentGapRatio : 0f;
        Vector2 textSize = AppFont.Measure(label, fontSize);
        float availableWidth = area.Width - HiddenRight - slant - area.Height * 0.2f;
        if (textSize.X > availableWidth)
        {
            fontSize *= availableWidth / textSize.X;
            textSize = AppFont.Measure(label, fontSize);
        }

        float groupHeight = iconSize + gap + textSize.Y;
        float top = area.Y + (area.Height - groupHeight) / 2f;
        float centerX = area.X + (area.Width - HiddenRight) / 2f;
        if (hasIcon)
        {
            Rectangle iconSource = new(0, 0, icon.Width, icon.Height);
            Rectangle iconTarget = new(centerX - iconSize / 2f, top, iconSize, iconSize);
            Raylib.DrawTexturePro(icon, iconSource, iconTarget, Vector2.Zero, 0f, white);
        }

        Vector2 textPosition = new(centerX - textSize.X / 2f, top + iconSize + gap);
        AppFont.Draw(label, textPosition, fontSize, white);
        if (HoldSeconds > 0f)
        {
            float inset = area.Height * 0.12f;
            float width = area.Width - slant - inset * 2f;
            float height = area.Height * 0.035f;
            Rectangle track = new(area.X + inset, area.Y + area.Height - height * 2f, width, height);
            Raylib.DrawRectangleRec(track, new Color(0, 0, 0, (int)(80 * alpha)));
            track.Width *= HoldProgress;
            Raylib.DrawRectangleRec(track, white);
        }
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
