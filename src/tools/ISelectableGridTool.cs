/// <summary>
/// Optional extension of IGridTool for tools that carry a "currently selected
/// thing" state which a click elsewhere should be able to cancel -- e.g.
/// FeaturePlacementTool holding a feature picked from a build menu.
/// GridInputHandler checks for this interface on right-click so it can clear
/// a selection without needing to know which concrete tool is active.
/// </summary>
public interface ISelectableGridTool : IGridTool
{
    /// <summary> True while this tool has something selected/armed. </summary>
    bool HasActiveSelection { get; }

    /// <summary> Cancels the current selection, returning the tool to its idle state. </summary>
    void ClearSelection();
}