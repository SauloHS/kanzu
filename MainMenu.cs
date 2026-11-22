using System.Numerics;
using Raylib_cs;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kanzu;

public class MainMenu
{
    private const float PulseStrength = 0.25f;
    private const float HoverScale = 1.55f;
    private const float HoverSpeed = 12f;
    private const float PulseRelease = 4f;
    private const float PeakDecay = 0.995f;
    private const float BassFilter = 0.02f;
    private const float LogoHeightRatio = 0.4f;

    private static volatile float bassEnergy;
    private static float lowPassState;
    private float peak;
    private float pulse;
    private float hoverBlend;
    private float logoScale = 1f;

    private Texture2D background;
    private Texture2D logo;
    private Music music;

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
    }

    public unsafe void Unload()
    {
        Raylib.DetachAudioMixedProcessor(&AnalyzeAudio);
        Raylib.UnloadTexture(background);
        Raylib.UnloadTexture(logo);
        Raylib.UnloadMusicStream(music);
    }

    private static string AssetPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", relativePath);
    }

    public void Draw()
    {
        DrawBackground();
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

    public void Update()
    {
        Raylib.UpdateMusicStream(music);

        float deltaTime = Raylib.GetFrameTime();
        bool hovering = IsMouseOverLogo();

        UpdatePulse(deltaTime);

        float hoverTarget = hovering ? 1f : 0f;
        hoverBlend += (hoverTarget - hoverBlend) * MathF.Min(1f, HoverSpeed * deltaTime);

        float pulseScale = 1f + PulseStrength * pulse;
        logoScale = pulseScale + (HoverScale - pulseScale) * hoverBlend;
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
        return Raylib.GetScreenHeight() * LogoHeightRatio;
    }

    private Vector2 ScreenCenter()
    {
        return new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
    }

    private void DrawLogo()
    {
        float size = BaseLogoSize() * logoScale;
        Vector2 center = ScreenCenter();
        Rectangle source = new(0, 0, logo.Width, logo.Height);
        Rectangle destination = new(center.X, center.Y, size, size);
        Vector2 origin = new(size / 2f, size / 2f);
        Raylib.DrawTexturePro(logo, source, destination, origin, 0f, Color.White);
    }

    private bool IsMouseOverLogo()
    {
        float radius = BaseLogoSize() / 2f;
        return Vector2.Distance(Raylib.GetMousePosition(), ScreenCenter()) <= radius;
    }
}