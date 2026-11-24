using Raylib_cs;

namespace Kanzu;

public static class EditorTheme
{
    public static readonly Color Background = new(24, 22, 33, 255);
    public static readonly Color Surface = new(35, 31, 47, 255);
    public static readonly Color Field = new(45, 40, 59, 255);
    public static readonly Color FieldFocus = new(52, 46, 69, 255);
    public static readonly Color Border = new(76, 67, 95, 255);
    public static readonly Color Accent = new(184, 169, 240, 255);
    public static readonly Color Primary = new(103, 79, 168, 255);
    public static readonly Color PrimaryHover = new(117, 90, 185, 255);
    public static readonly Color SecondaryText = new(203, 195, 221, 255);
    public static readonly Color Placeholder = new(177, 166, 196, 255);
    public static readonly Color Success = new(168, 230, 207, 255);
    public static readonly Color Error = new(255, 174, 188, 255);

    public static float Roundness(Rectangle bounds, float radius)
    {
        return Math.Clamp(radius * 2f / MathF.Max(1f, MathF.Min(bounds.Width, bounds.Height)), 0f, 1f);
    }
}
