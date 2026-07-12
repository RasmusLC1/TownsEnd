using Godot;

/// <summary>
/// Drives the day/night cycle's timing and exposes the current time of day
/// to gameplay systems. Controls TWO AnimationPlayers -- sky color and sun
/// rotation -- applying the same SpeedScale to both so they stay in sync
/// </summary>
public partial class DayNightCycle : Node
{
    [Export] public AnimationPlayer SkyAnimationPlayer { get; set; }
    [Export] public string SkyAnimationName { get; set; } = "SkyColor";

    [Export] public AnimationPlayer SunAnimationPlayer { get; set; }
    [Export] public string SunAnimationName { get; set; } = "SunRotate";

    private float _dayLengthSeconds = 600f; // 5 minutes = 300

    /// <summary> How long a full day/night cycle takes in real seconds. </summary>
    [Export]
    public float DayLengthSeconds
    {
        get => _dayLengthSeconds;
        set { _dayLengthSeconds = value; ApplySpeedScale(); }
    }

    // Normalized time (0-1) at which night is considered to start/end --
    // line these up with wherever your animations' darkest keyframes sit.
    [Export(PropertyHint.Range, "0,1")] public float NightStart { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0,1")] public float NightEnd { get; set; } = 0.25f;

    [Signal] public delegate void TimeOfDayChangedEventHandler(float normalizedTime);
    [Signal] public delegate void NightStartedEventHandler();
    [Signal] public delegate void DayStartedEventHandler();

    private bool _wasNight = false;

    /// <summary> 0.0 to 1.0 -- where the cycle currently is. </summary>
    public float NormalizedTime { get; private set; }

    /// <summary> 0.0 to 24.0 -- the same position expressed as an in-game hour. </summary>
    public float CurrentHour => NormalizedTime * 24.0f;

    public override void _Ready()
    {
        bool skyOk = ValidatePlayer(SkyAnimationPlayer, SkyAnimationName, "Sky");
        bool sunOk = ValidatePlayer(SunAnimationPlayer, SunAnimationName, "Sun");

        if (!skyOk && !sunOk)
        {
            GD.PrintErr("[DayNightCycle] No valid animation players found -- nothing to drive.");
            return;
        }

        ApplySpeedScale();

        if (skyOk) SkyAnimationPlayer.Play(SkyAnimationName);
        if (sunOk) SunAnimationPlayer.Play(SunAnimationName);
    }

    private bool ValidatePlayer(AnimationPlayer player, string animName, string label)
    {
        if (player == null)
        {
            GD.PrintErr($"[DayNightCycle] No {label} AnimationPlayer assigned.");
            return false;
        }

        if (!player.HasAnimation(animName))
        {
            string available = string.Join(", ", player.GetAnimationList());
            GD.PrintErr($"[DayNightCycle] No {label} animation named '{animName}' found. Available: [{available}]");
            return false;
        }

        return true;
    }

    private void ApplySpeedScale()
    {
        float? scale = ComputeSpeedScale(SkyAnimationPlayer, SkyAnimationName)
                    ?? ComputeSpeedScale(SunAnimationPlayer, SunAnimationName);

        if (scale == null) return;

        if (SkyAnimationPlayer != null) SkyAnimationPlayer.SpeedScale = scale.Value;
        if (SunAnimationPlayer != null) SunAnimationPlayer.SpeedScale = scale.Value;
    }

    private float? ComputeSpeedScale(AnimationPlayer player, string animName)
    {
        if (player == null || !player.HasAnimation(animName)) return null;

        Animation animation = player.GetAnimation(animName);

        // Small check to veryify animation
        if (animation == null || animation.Length <= 0 || DayLengthSeconds <= 0) return null;

        return (float)(animation.Length / DayLengthSeconds);
    }

    public override void _Process(double delta)
    {
        // Sky is the reference clock; falls back to Sun if only that's assigned.
        AnimationPlayer reference = SkyAnimationPlayer ?? SunAnimationPlayer;
        if (reference == null) return;

        double animLength = reference.CurrentAnimationLength;
        if (animLength <= 0) return;

        NormalizedTime = (float)(reference.CurrentAnimationPosition / animLength);
        EmitSignal(SignalName.TimeOfDayChanged, NormalizedTime);

        bool isNightNow = IsNight();
        if (isNightNow != _wasNight)
        {
            EmitSignal(isNightNow ? SignalName.NightStarted : SignalName.DayStarted);
            _wasNight = isNightNow;
        }
    }

    /// <summary> Handles night ranges that wrap across midnight (e.g. 0.75 -> 0.25). </summary>
    public bool IsNight()
    {
        if (NightStart > NightEnd)
            return NormalizedTime >= NightStart || NormalizedTime <= NightEnd;
        return NormalizedTime >= NightStart && NormalizedTime <= NightEnd;
    }

    public bool IsDay() => !IsNight();

    /// <summary>
    /// Jumps both animations to a specific point in the cycle (0-1),
    /// e.g. for a "sleep until morning" mechanic or work day done.
    /// </summary>
    public void SetNormalizedTime(float t)
    {
        SeekPlayer(SkyAnimationPlayer, SkyAnimationName, t);
        SeekPlayer(SunAnimationPlayer, SunAnimationName, t);
        NormalizedTime = t;
    }

    private void SeekPlayer(AnimationPlayer player, string animName, float t)
    {
        if (player == null) return;

        Animation anim = player.GetAnimation(animName);
        if (anim == null || anim.Length <= 0) return;

        player.Seek(t * anim.Length, update: true);
    }
}