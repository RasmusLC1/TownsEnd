using Godot;
using System;

public partial class Tree : Node3D
{
    // The type of Tree (e.g., "oak", "pine", "birch")
    [Export] public string Type { get; set; } = "oak";

    // The amount of resources held inside the tree (also drives growth stage)
    private int _size = 1;
    // Growth tuning
    [Export] public int MaxSize { get; set; } = 100;

    [Export]
    public int Size
    {
        get => _size;
        set => _size = Mathf.Clamp(value, 0, MaxSize);
    }

     // Grow the tree every 10 sets
    [Export] public int GrowthStep { get; set; } = 10;
    [Export] public double GrowthIntervalSeconds { get; set; } = 1.0;

    // The visual mesh/model to scale — assign in the editor, or grab by name
    [Export] public Node3D Model { get; set; }

    private Timer _growthTimer;

    public override void _Ready()
    {
        // Fallback if Model wasn't wired up in the inspector
        Model ??= GetNodeOrNull<Node3D>("Model");


        ApplyScaleForSize(); // set correct scale immediately (e.g. on load/spawn)

        if (Size > MaxSize) return; // Return if max size reached
        
        _growthTimer = new Timer
        {
            WaitTime = GrowthIntervalSeconds,
            Autostart = true,
            OneShot = false
        };
        AddChild(_growthTimer);
        _growthTimer.Timeout += OnGrowthTick;
    }

    private void OnGrowthTick()
    {
        Size = Math.Min(Size + GrowthStep, MaxSize);
        AnimateToCurrentScale();

        if (Size >= MaxSize)
            _growthTimer.Stop(); // done growing, no more ticks needed
    }

    // Instant scale set (used on _Ready so trees loaded at size 60 don't animate from 0)
    private void ApplyScaleForSize()
    {
        if (Model == null) return;
        float t = (float)Size / MaxSize;
        Model.Scale = Vector3.One * t;
    }

    // Smoothly animates to the new scale after a growth tick
    private void AnimateToCurrentScale()
    {
        if (Model == null) return;
        float t = (float)Size / MaxSize;

        var tween = CreateTween();
        tween.TweenProperty(Model, "scale", Vector3.One * t, 0.5)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.Out);
    }


}