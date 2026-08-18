/// <summary>
/// Allows features to spread, for example plants, water or fire
/// </summary>

public interface ISpreadable
{
    double SpreadIntervalSeconds { get; set; }
    bool CanSpread { get; }
    void Spread();
}