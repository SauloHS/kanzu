using System.Numerics;
using Raylib_cs;

namespace Kanzu;

public class TextField
{
    private const float BlinkSeconds = 0.5f;

    private readonly string placeholder;
    private readonly int maxLength;
    private readonly bool multiline;
    private float blinkTimer;
    private float horizontalScroll;
    private float verticalScroll;
    private string text = "";
    private int caret;
    private int anchor;
    private bool wasHovering;
    private bool hovering;

    public string Text
    {
        get => text;
        set
        {
            text = value ?? "";
            caret = text.Length;
            anchor = caret;
        }
    }

    public Rectangle Bounds { get; set; }
    public float FontSize { get; set; } = 18f;
    public float Padding { get; set; } = 12f;
    public bool DigitsOnly { get; set; }
    public bool Invalid { get; set; }
    public bool Focused { get; private set; }

    public TextField(string placeholder, int maxLength, bool multiline = false)
    {
        this.placeholder = placeholder;
        this.maxLength = maxLength;
        this.multiline = multiline;
    }

    public void Focus(bool focused)
    {
        if (Focused == focused)
        {
            return;
        }

        Focused = focused;
        blinkTimer = 0f;
        if (!focused)
        {
            anchor = caret;
        }
    }

    public void Update(float deltaTime)
    {
        hovering = Raylib.IsWindowFocused() && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
        if (hovering && !wasHovering)
        {
            Sfx.PlayHover();
        }

        wasHovering = hovering;
        if (!Raylib.IsWindowFocused())
        {
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            bool inside = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
            Focus(inside);
            if (inside)
            {
                PlaceCaret(Raylib.GetMousePosition());
                Sfx.PlayClick();
            }
        }

        if (!Focused)
        {
            return;
        }

        blinkTimer += deltaTime;
        ReadKeyboard();
        if (multiline && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds))
        {
            verticalScroll = MathF.Max(0f, verticalScroll - Raylib.GetMouseWheelMove() * LineHeight * 2f);
        }
    }

    private bool ControlHeld => Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
    private bool ShiftHeld => Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
    private float LineHeight => FontSize * 1.45f;
    private float ContentWidth => MathF.Max(1f, Bounds.Width - Padding * 2f);
    private int SelectionStart => Math.Min(caret, anchor);
    private int SelectionLength => Math.Abs(caret - anchor);

    private static bool Pressed(KeyboardKey key)
    {
        return Raylib.IsKeyPressed(key) || Raylib.IsKeyPressedRepeat(key);
    }

    private void ReadKeyboard()
    {
        caret = Math.Clamp(caret, 0, text.Length);
        anchor = Math.Clamp(anchor, 0, text.Length);
        if (ControlHeld)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.A))
            {
                anchor = 0;
                caret = text.Length;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.C) && SelectionLength > 0)
            {
                Raylib.SetClipboardText(text.Substring(SelectionStart, SelectionLength));
            }

            if (Raylib.IsKeyPressed(KeyboardKey.X) && SelectionLength > 0)
            {
                Raylib.SetClipboardText(text.Substring(SelectionStart, SelectionLength));
                ReplaceSelection("");
            }

            if (Raylib.IsKeyPressed(KeyboardKey.V))
            {
                Insert(Raylib.GetClipboardText_() ?? "");
            }
        }

        if (Pressed(KeyboardKey.Left))
        {
            MoveCaret(!ShiftHeld && SelectionLength > 0 ? SelectionStart : caret - 1);
        }

        if (Pressed(KeyboardKey.Right))
        {
            MoveCaret(!ShiftHeld && SelectionLength > 0 ? SelectionStart + SelectionLength : caret + 1);
        }

        if (Pressed(KeyboardKey.Home))
        {
            MoveCaret(ControlHeld ? 0 : CurrentLineStart());
        }

        if (Pressed(KeyboardKey.End))
        {
            MoveCaret(ControlHeld ? text.Length : CurrentLineEnd());
        }

        if (multiline && Pressed(KeyboardKey.Up))
        {
            MoveVertical(-1);
        }

        if (multiline && Pressed(KeyboardKey.Down))
        {
            MoveVertical(1);
        }

        if (Pressed(KeyboardKey.Backspace))
        {
            if (SelectionLength > 0)
            {
                ReplaceSelection("");
            }
            else if (caret > 0)
            {
                anchor = caret - 1;
                ReplaceSelection("");
            }
        }

        if (Pressed(KeyboardKey.Delete))
        {
            if (SelectionLength > 0)
            {
                ReplaceSelection("");
            }
            else if (caret < text.Length)
            {
                anchor = caret + 1;
                ReplaceSelection("");
            }
        }

        if (multiline && Pressed(KeyboardKey.Enter))
        {
            Insert("\n");
        }

        int codepoint;
        while ((codepoint = Raylib.GetCharPressed()) > 0)
        {
            if (!ControlHeld && AppFont.Supports(codepoint))
            {
                Insert(char.ConvertFromUtf32(codepoint));
            }
        }
    }

    private void Insert(string value)
    {
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        string accepted = new(value.Where(c => (multiline && c == '\n') ||
            (AppFont.Supports(c) && (!DigitsOnly || char.IsAsciiDigit(c)))).ToArray());
        int available = maxLength - text.Length + SelectionLength;
        if (available <= 0 || accepted.Length == 0)
        {
            return;
        }

        ReplaceSelection(accepted[..Math.Min(available, accepted.Length)]);
    }

    private void ReplaceSelection(string value)
    {
        int start = SelectionStart;
        text = text.Remove(start, SelectionLength).Insert(start, value);
        caret = start + value.Length;
        anchor = caret;
        blinkTimer = 0f;
    }

    private void MoveCaret(int position)
    {
        caret = Math.Clamp(position, 0, text.Length);
        if (!ShiftHeld)
        {
            anchor = caret;
        }

        blinkTimer = 0f;
    }

    private int CurrentLineStart()
    {
        return caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
    }

    private int CurrentLineEnd()
    {
        int next = text.IndexOf('\n', caret);
        return next < 0 ? text.Length : next;
    }

    private List<(int Start, int End)> Lines()
    {
        List<(int Start, int End)> lines = new();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add((start, i));
                start = i + 1;
                continue;
            }

            if (multiline && i > start && AppFont.Measure(text[start..(i + 1)], FontSize).X > ContentWidth)
            {
                lines.Add((start, i));
                start = i;
            }
        }

        lines.Add((start, text.Length));
        return lines;
    }

    private int CaretLine(List<(int Start, int End)> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++)
        {
            if (caret < lines[i + 1].Start)
            {
                return i;
            }
        }

        return lines.Count - 1;
    }

    private void MoveVertical(int direction)
    {
        List<(int Start, int End)> lines = Lines();
        int line = CaretLine(lines);
        float x = AppFont.Measure(text[lines[line].Start..caret], FontSize).X;
        int target = Math.Clamp(line + direction, 0, lines.Count - 1);
        MoveCaret(NearestCaret(lines[target], x));
    }

    private int NearestCaret((int Start, int End) line, float x)
    {
        float previous = 0f;
        for (int i = line.Start; i < line.End; i++)
        {
            float next = AppFont.Measure(text[line.Start..(i + 1)], FontSize).X;
            if (x < (previous + next) / 2f)
            {
                return i;
            }

            previous = next;
        }

        return line.End;
    }

    private void PlaceCaret(Vector2 mouse)
    {
        List<(int Start, int End)> lines = Lines();
        float y = mouse.Y - Bounds.Y - Padding + verticalScroll;
        int line = multiline ? Math.Clamp((int)(y / LineHeight), 0, lines.Count - 1) : 0;
        caret = NearestCaret(lines[line], mouse.X - Bounds.X - Padding + horizontalScroll);
        anchor = caret;
        blinkTimer = 0f;
    }

    public void Draw()
    {
        float scale = FontSize / 18f;
        float roundness = EditorTheme.Roundness(Bounds, 6f * scale);
        bool highlighted = Focused || hovering;
        Color border = Invalid ? EditorTheme.Error : highlighted ? EditorTheme.Accent : EditorTheme.Border;
        Raylib.DrawRectangleRounded(Bounds, roundness, 12, highlighted ? EditorTheme.FieldFocus : EditorTheme.Field);
        Raylib.DrawRectangleRoundedLinesEx(Bounds, roundness, 12, highlighted ? 2f * scale : scale, border);

        List<(int Start, int End)> lines = Lines();
        int caretLine = CaretLine(lines);
        float caretX = AppFont.Measure(text[lines[caretLine].Start..caret], FontSize).X;
        float height = MathF.Max(1f, Bounds.Height - Padding * 2f);
        if (Focused && !multiline)
        {
            horizontalScroll = Math.Clamp(horizontalScroll, MathF.Max(0f, caretX - ContentWidth + 3f * scale), caretX);
        }
        else if (multiline)
        {
            horizontalScroll = 0f;
            if (Focused)
            {
                verticalScroll = Math.Clamp(verticalScroll,
                    MathF.Max(0f, (caretLine + 1) * LineHeight - height), caretLine * LineHeight);
            }

            verticalScroll = Math.Clamp(verticalScroll, 0f, MathF.Max(0f, lines.Count * LineHeight - height));
        }

        float x = Bounds.X + Padding - horizontalScroll;
        float top = multiline ? Bounds.Y + Padding - verticalScroll : Bounds.Y + (Bounds.Height - AppFont.Measure("A", FontSize).Y) / 2f;
        Raylib.BeginScissorMode((int)(Bounds.X + Padding), (int)(Bounds.Y + (multiline ? Padding : 2f * scale)),
            (int)ContentWidth, (int)(multiline ? height : Bounds.Height - 4f * scale));

        if (text.Length == 0 && !Focused)
        {
            AppFont.Draw(placeholder, new Vector2(x, top), FontSize, EditorTheme.Placeholder);
        }
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                float y = top + i * LineHeight;
                if (y + LineHeight < Bounds.Y || y > Bounds.Y + Bounds.Height)
                {
                    continue;
                }

                DrawSelection(lines[i], x, y);
                AppFont.Draw(text[lines[i].Start..lines[i].End], new Vector2(x, y), FontSize, Color.White);
            }
        }

        if (Focused && (int)(blinkTimer / BlinkSeconds) % 2 == 0)
        {
            Raylib.DrawRectangleRec(new Rectangle(x + caretX, top + caretLine * LineHeight, 2f * scale, FontSize), Color.White);
        }

        Raylib.EndScissorMode();
    }

    private void DrawSelection((int Start, int End) line, float x, float y)
    {
        int start = Math.Max(SelectionStart, line.Start);
        int end = Math.Min(SelectionStart + SelectionLength, line.End);
        if (!Focused || start >= end)
        {
            return;
        }

        float left = AppFont.Measure(text[line.Start..start], FontSize).X;
        float width = AppFont.Measure(text[start..end], FontSize).X;
        Raylib.DrawRectangleRec(new Rectangle(x + left, y, width, LineHeight), new Color(103, 79, 168, 170));
    }
}
