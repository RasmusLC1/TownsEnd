using Godot;
using System;

[Tool]
public partial class Plant : Node3D
{
    [Export] public string Type { get; set; } = "generic";
    [Export] public double GrowthIntervalSeconds { get; set; } = 1.0;
    [Export] public int GrowthStep { get; set; } = 10;
    [Export] public Node3D Model { get; set; }
    [Export] public int MaxSize = 100;
    public IslandTile OccupiedTile { get; set; }

    private int _size = 1;

    [Export]
    public int Size
    {
        get => _size;
        set => _size = Mathf.Clamp(value, 0, MaxSize);
    }

    private Timer _growthTimer;

    public override void _Ready()
    {
        Model ??= GetNodeOrNull<Node3D>("Model");
        ApplyScaleForSize();

        if (Size >= MaxSize) return;

       SetupGrowthTimer();
    }

    private void SetupGrowthTimer()
    {
         _growthTimer = new Timer
        {
            WaitTime = GrowthIntervalSeconds,
            Autostart = true,
            OneShot = false
        };
        AddChild(_growthTimer);
        _growthTimer.Timeout += OnGrowthTick;
    }

    protected virtual void OnGrowthTick()
    {
        Size = Math.Min(Size + GrowthStep, MaxSize);
        AnimateToCurrentScale();

        if (Size >= MaxSize)
        {
            _growthTimer.Stop();
            OnMatured();
        }
    }

    private void ApplyScaleForSize()
    {
        if (Model == null) return;
        Model.Scale = Vector3.One * ((float)Size / MaxSize);
    }

    private void AnimateToCurrentScale()
    {
        if (Model == null) return;
        var tween = CreateTween();
        tween.TweenProperty(Model, "scale", Vector3.One * ((float)Size / MaxSize), 0.5)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.Out);
    }

    protected virtual void OnMatured() { }
}