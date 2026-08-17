namespace Osu.Performance;

/// <summary> The state of a score, used as input for performance calculation. </summary>
public struct PerformanceScore
{
    public uint MaxCombo;
    public uint PassedObjects;

    public uint Count300;
    public uint Count100;
    public uint Count50;
    public uint CountMiss;
    public uint CountKatu;
    public uint CountGeki;

    public double Accuracy;
    public long? LegacyTotalScore;

    public uint CountSmallTickMiss;
    public uint CountSmallTickHit;
    public uint CountLargeTickMiss;
    public uint CountLargeTickHit;
    public uint CountSliderTailHit;
}