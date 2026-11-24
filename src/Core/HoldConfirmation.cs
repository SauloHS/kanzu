namespace Kanzu;

public sealed class HoldConfirmation
{
    private float elapsed;
    private bool active;
    private bool confirmed;

    public float Progress { get; private set; }
    public float VisualProgress => (1f - MathF.Exp(-5f * Progress)) / (1f - MathF.Exp(-5f));

    public bool Update(float deltaTime, bool pressed, bool held, float seconds)
    {
        if (!held)
        {
            Reset();
            return false;
        }

        if (pressed)
        {
            Reset();
            active = true;
            return false;
        }

        if (!active || confirmed)
        {
            return false;
        }

        elapsed += MathF.Max(0f, deltaTime);
        Progress = Math.Clamp(elapsed / seconds, 0f, 1f);
        confirmed = elapsed >= seconds;
        return confirmed;
    }

    public void Reset()
    {
        elapsed = 0f;
        Progress = 0f;
        active = false;
        confirmed = false;
    }
}
