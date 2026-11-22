using System.Numerics;
using Raylib_cs;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kanzu;

public class MainMenu : IScreen
{
    private const float PulseStrength = 0.25f;
    private const float HoverScale = 1.55f;
    private const float HoverSpeed = 12f;
    private const float PulseRelease = 4f;
    private const float PeakDecay = 0.995f;
    private const float BassFilter = 0.02f;
    private const float LogoHeightRatio = 0.4f;
    private const float LogoOpenHeightRatio = 0.22f;
    private const float LogoClosedX = 0.5f;
    private const float LogoOpenX = 0.25f;
    private const float OpenSpeed = 10f;

    private bool menuOpen;
    private float openBlend; // 0 = closed, 1 = openBlend

    private static volatile float bassEnergy;
    private static float lowPassState;
    private float peak;
    private float pulse;
    private float hoverBlend;
    private float logoScale = 1f;

    private const float BarHeightRatio = 0.14f;
    private const float BarOpacity = 0.6f;

    private const float ButtonAspect = 2.2f;
    private const float ButtonGapRatio = 0.12f;
    private const float ButtonsMarginRatio = 0.025f;

    private readonly List<MenuButton> buttons;
    private readonly MenuButton settingsButton;
    private readonly Game game;

    private Texture2D background;
    private Texture2D logo;
    private Music music;

    public MainMenu(Game game, bool startOpen = false)
    {
        this.game = game;
        menuOpen = startOpen;
        openBlend = startOpen ? 1f : 0f;
        settingsButton = new MenuButton("Settings", "icons/settings.png", new Color(92, 87, 85, 255), null);

        buttons = new List<MenuButton>
        {
            new MenuButton("Play", "icons/play.png", new Color(168, 230, 207, 255), null),
            new MenuButton("Editor", "icons/editor.png", new Color(169, 214, 245, 255), () => game.ChangeScreen(new EditorScreen(game))),
            new MenuButton("Account", "icons/account.png", new Color(255, 201, 168, 255), null),
            new MenuButton("Exit", "icons/exit.png", new Color(244, 169, 184, 255), () => game.Exit())
        };
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void AnalyzeAudio(void* bufferData, uint frames)
    {
        float* samples = (float*)bufferData;
        float sum = 0f;

        for (int i = 0; i < frames; i++)
        {
            float mono = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
            lowPassState += BassFilter * (mono - lowPassState);
            sum += lowPassState * lowPassState;
        }

        bassEnergy = MathF.Sqrt(sum / frames);
    }

    private void LayoutButtons()
    {
        float screenWidth = Raylib.GetScreenWidth();
        float screenHeight = Raylib.GetScreenHeight();
        float height = screenHeight * BarHeightRatio;
        float slant = height * MenuButton.SlantRatio;
        float gap = height * ButtonGapRatio;
        float top = (screenHeight - height) / 2f;
        float slide = (1f - openBlend) * screenWidth * 0.04f;

        float logoCenterX = screenWidth * LogoOpenX;
        float logoRadius = screenHeight * LogoOpenHeightRatio / 2f;

        float start = logoCenterX + logoRadius + screenWidth * ButtonsMarginRatio;
        float available = screenWidth - start - screenWidth * ButtonsMarginRatio;
        float width = MathF.Min(height * ButtonAspect, (available + (buttons.Count - 1) * (slant - gap)) / buttons.Count);
        float step = width - slant + gap;

        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].Bounds = new Rectangle(start + slide + i * step, top, width, height);
        }

        float settingsWidth = width + logoRadius;
        settingsButton.HiddenRight = logoRadius;
        settingsButton.Bounds = new Rectangle(logoCenterX - settingsWidth + slide, top, settingsWidth, height);
    }

    public unsafe void Load()
    {
        int index = Random.Shared.Next(1, 4);
        background = Raylib.LoadTexture(AssetPath($"main-menu/{index}.png"));
        logo = Raylib.LoadTexture(AssetPath("logo.png"));
        Raylib.SetTextureFilter(background, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(logo, TextureFilter.Bilinear);

        music = Raylib.LoadMusicStream(AssetPath("main-menu/music.mp3"));
        music.Looping = true;
        Raylib.PlayMusicStream(music);
        Raylib.AttachAudioMixedProcessor(&AnalyzeAudio);

        settingsButton.Load();

        foreach (MenuButton button in buttons)
        {
            button.Load();
        }
    }

    public unsafe void Unload()
    {
        Raylib.DetachAudioMixedProcessor(&AnalyzeAudio);
        Raylib.UnloadTexture(background);
        Raylib.UnloadTexture(logo);
        Raylib.UnloadMusicStream(music);

        settingsButton.Unload();

        foreach (MenuButton button in buttons)
        {
            button.Unload();
        }
    }

    public static string AssetPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);
    }

    public void Draw()
    {
        DrawBackground();
        DrawBar();
        DrawButtons();
        DrawLogo();
    }

    private void DrawBackground()
    {
        float screenWidth = Raylib.GetScreenWidth();
        float screenHeight = Raylib.GetScreenHeight();
        float scale = MathF.Max(screenWidth / background.Width, screenHeight / background.Height);
        float width = background.Width * scale;
        float height = background.Height * scale;

        Rectangle source = new(0, 0, background.Width, background.Height);
        Rectangle destination = new((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
        Raylib.DrawTexturePro(background, source, destination, Vector2.Zero, 0f, Color.White);
    }

    private void DrawBar()
    {
        float screenHeight = Raylib.GetScreenHeight();
        float height = screenHeight * BarHeightRatio * openBlend;
        float top = (screenHeight - height) / 2f;
        byte opacity = (byte)(255 * BarOpacity * openBlend);
        Color barColor = new((byte)255, (byte)255, (byte)255, opacity);
        Raylib.DrawRectangle(0, (int)top, Raylib.GetScreenWidth(), (int)height, barColor);
    }

    public void Update()
    {
        Raylib.UpdateMusicStream(music);

        float deltaTime = Raylib.GetFrameTime();
        bool hovering = IsMouseOverLogo();

        if (hovering && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            menuOpen = !menuOpen;
        }

        openBlend += ((menuOpen ? 1f : 0f) - openBlend) * MathF.Min(1f, OpenSpeed * deltaTime);

        UpdatePulse(deltaTime);

        float hoverTarget = hovering ? 1f : 0f;
        hoverBlend += (hoverTarget - hoverBlend) * MathF.Min(1f, HoverSpeed * deltaTime);

        float pulseScale = 1f + PulseStrength * pulse;
        logoScale = pulseScale + (HoverScale - pulseScale) * hoverBlend;

        LayoutButtons();

        bool interactive = menuOpen && !hovering;
        settingsButton.Update(deltaTime, interactive);

        foreach (MenuButton button in buttons)
        {
            button.Update(deltaTime, interactive);
        }
    }

    private void DrawButtons()
    {
        if (openBlend < 0.01f)
        {
            return;
        }

        settingsButton.Draw(openBlend);

        foreach (MenuButton button in buttons)
        {
            button.Draw(openBlend);
        }
    }

    private void UpdatePulse(float deltaTime)
    {
        float energy = bassEnergy;
        peak = MathF.Max(energy, peak * PeakDecay);
        float level = MathF.Pow(energy / MathF.Max(peak, 0.0001f), 4f);
        pulse = MathF.Max(level, pulse - PulseRelease * deltaTime);
    }

    private float BaseLogoSize()
    {
        float screenHeight = Raylib.GetScreenHeight();
        float closedSize = screenHeight * LogoHeightRatio;
        float openSize = screenHeight * LogoOpenHeightRatio;
        return closedSize + (openSize - closedSize) * openBlend;
    }

    private Vector2 LogoCenter()
    {
        float closedX = Raylib.GetScreenWidth() * LogoClosedX;
        float openX = Raylib.GetScreenWidth() * LogoOpenX;
        return new Vector2(closedX + (openX - closedX) * openBlend, Raylib.GetScreenHeight() / 2f);
    }
    private void DrawLogo()
    {
        float size = BaseLogoSize() * logoScale;
        Vector2 center = LogoCenter();
        Rectangle source = new(0, 0, logo.Width, logo.Height);
        Rectangle destination = new(center.X, center.Y, size, size);
        Vector2 origin = new(size / 2f, size / 2f);
        Raylib.DrawTexturePro(logo, source, destination, origin, 0f, Color.White);
    }

    private bool IsMouseOverLogo()
    {
        float radius = BaseLogoSize() / 2f;
        return Vector2.Distance(Raylib.GetMousePosition(), LogoCenter()) <= radius;
    }
}
