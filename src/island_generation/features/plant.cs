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
    [Export] public int FoodValue = 5;
    [Export] public int FoodValueMax = 100;
    [Export] public bool DestroyOnHarvest = true;

    public IslandTile OccupiedTile { get; set; }
    [Export] public PackedScene Scene { get; set; } // assign the Tree's own scene in the inspector
    public IslandGenerator Generator; // set by TreeSpawner when this tree is spawned, like OccupiedTile



    private int _size = 1;

    [Export]
    public int Size
    {
        get => _size;
        set => _size = Mathf.Clamp(value, 0, MaxSize);
    }

    private Timer _growthTimer;
    private Timer _foodTimer;

    public override void _Ready()
    {
        Model ??= GetNodeOrNull<Node3D>("Model");
        ApplyScaleForSize();

        if (Size >= MaxSize) return;

       SetupGrowthTimer();
    }

    private void SetupGrowthTimer()
    {
        _growthTimer = CreateTicker(GrowthIntervalSeconds, OnGrowthTick);
    }
    private void TriggerFoodTimer()
    {
        if (0 == FoodValueMax) return;
        _foodTimer = CreateTicker(GrowthIntervalSeconds, OnFoodTick);
    }

    protected virtual void OnGrowthTick()
    {
        Size = Math.Min(Size + GrowthStep, MaxSize);
        AnimateToCurrentScale();

        if (Size >= MaxSize)
        {
            _growthTimer.Stop();
            OnMatured();
            TriggerFoodTimer();
        }
    }

    private Timer CreateTicker(double interval, Action onTick)
    {
        var t = new Timer { WaitTime = interval, Autostart = true, OneShot = false };
        AddChild(t);
        t.Timeout += onTick;
        return t;
    }

    protected virtual void OnFoodTick()
    {
        FoodValue = Math.Min(FoodValue + GrowthStep, FoodValueMax);
        AnimateToCurrentScale();

        if (FoodValue >= FoodValueMax)
        {
            _foodTimer.Stop();
        }
    }

    private void Destroy()
    {
        // Stop the timer first so it doesn't try to tick after we're gone
        _growthTimer?.Stop();
        _foodTimer?.Stop();

        // Let the tile know it's no longer occupied
        OccupiedTile?.ClearOccupyingObject(); // or whatever your IslandTile's API is

        QueueFree();
    }

    private void ApplyScaleForSize()
    {
        if (Model == null) return;
        Model.Scale = Vector3.One * ((float)Size / MaxSize);
    }
    public int Harvest()
    {
        int harvestValue = FoodValue;
        FoodValue = 0;
        if (DestroyOnHarvest) Destroy();
        else _foodTimer?.Start();
        return harvestValue;
    }

    private void AnimateToCurrentScale()
    {
        if (Model == null) return;
        var tween = CreateTween();
        tween.TweenProperty(Model, "scale", Vector3.One * ((float)Size / MaxSize), 0.5)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.Out);
    }

    protected virtual void OnMatured()
    {
    }
}