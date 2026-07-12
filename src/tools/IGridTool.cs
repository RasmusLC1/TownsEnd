using Godot;

/// <summary>
/// A tool that reacts to a rectangular grid selection made via drag (or a
/// single click, which is just a selection where start == end). Implement
/// this for each distinct mouse-driven action -- deleting tiles, placing a
/// feature, painting terrain, etc. GridInputHandler never needs to know
/// which one it's talking to.
/// </summary>
public interface IGridTool
{
    /// <summary> Outline color shown while dragging with this tool active. </summary>
    Color OutlineColor { get; }

    /// <summary> Called once when the drag/click completes. </summary>
    void OnAreaSelected(Vector2I start, Vector2I end);
}