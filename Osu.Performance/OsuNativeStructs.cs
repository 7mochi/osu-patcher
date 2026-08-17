using System;
using System.Runtime.InteropServices;
using Osu.Performance;

namespace Osu.Performance;

internal struct NativeRuleset
{
    public uint Handle;
    public int RulesetId;
}

internal struct NativeModsCollection
{
    public uint Handle;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct NativeNullableInt64
{
    [FieldOffset(0)]
    public byte HasValue;

    [FieldOffset(8)]
    public long Value;

    public static NativeNullableInt64 FromNullable(long? value) =>
        value.HasValue
            ? new NativeNullableInt64 { HasValue = 1, Value = value.Value }
            : default;
}

internal struct NativeScoreInfo
{
    public uint RulesetHandle;
    public uint BeatmapHandle;
    public uint ModsHandle;
    public int MaxCombo;
    public double Accuracy;
    public NativeNullableInt64 LegacyTotalScore;
    public int CountMiss;
    public int CountMeh;
    public int CountOk;
    public int CountGood;
    public int CountGreat;
    public int CountPerfect;
    public int CountSmallTickMiss;
    public int CountSmallTickHit;
    public int CountLargeTickMiss;
    public int CountLargeTickHit;
    public int CountSliderTailHit;
}

internal struct NativeOsuDifficultyAttributes
{
    public double StarRating;
    public int MaxCombo;
    public double AimDifficulty;
    public double AimDifficultSliderCount;
    public double SpeedDifficulty;
    public double SpeedNoteCount;
    public double FlashlightDifficulty;
    public double ReadingDifficulty;
    public double SliderFactor;
    public double AimTopWeightedSliderFactor;
    public double SpeedTopWeightedSliderFactor;
    public double AimDifficultStrainCount;
    public double SpeedDifficultStrainCount;
    public double ReadingDifficultNoteCount;
    public double NestedScorePerObject;
    public double LegacyScoreBaseMultiplier;
    public double MaximumLegacyComboScore;
    public int HitCircleCount;
    public int SliderCount;
    public int SpinnerCount;
}

internal struct NativeTimedOsuDifficultyAttributes
{
    public double Time;
    public NativeOsuDifficultyAttributes Attributes;
}

internal struct NativeOsuPerformanceAttributes
{
    public double Total;
    public double Aim;
    public double Speed;
    public double Accuracy;
    public double Flashlight;
    public double Reading;
    public double EffectiveMissCount;
    public NativeNullableDouble SpeedDeviation;
    public double ComboBasedEstimatedMissCount;
    public NativeNullableDouble ScoreBasedEstimatedMissCount;
    public double AimEstimatedSliderBreaks;
    public double SpeedEstimatedSliderBreaks;
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct NativeNullableDouble
{
    [FieldOffset(0)]
    public byte HasValue;

    [FieldOffset(8)]
    public double Value;
}

internal struct NativeBeatmap : IDisposable
{
    public uint Handle;
    public int RulesetId;
    public int BeatmapId;
    public float ApproachRate;
    public float DrainRate;
    public float OverallDifficulty;
    public float CircleSize;
    public double SliderMultiplier;
    public double SliderTickRate;

    public static NativeBeatmap Create(byte[] content)
    {
        unsafe
        {
            NativeBeatmap beatmap;
            fixed (byte* contentPtr = content)
            {
                OsuNativeCalls.Check(OsuNativeCalls.Beatmap_CreateFromText(contentPtr, &beatmap));
            }
            return beatmap;
        }
    }

    public void Dispose() => OsuNativeCalls.Check(OsuNativeCalls.Beatmap_Destroy(Handle));
}