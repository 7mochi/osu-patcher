using System;

namespace Osu.Performance;

/// <summary>
///     A gradual osu! performance calculator: pre-computes the per-object difficulty attributes
///     once, then recalculates performance for the current score as the playhead advances.
/// </summary>
internal sealed class OsuGradualPerformance : IDisposable
{
    private readonly uint _rulesetHandle;
    private readonly uint _modsHandle;
    private readonly uint _beatmapHandle;
    private readonly uint _difficultyHandle;
    private readonly uint _performanceHandle;

    private readonly NativeTimedOsuDifficultyAttributes[] _timedAttributes;
    private int _lastIndex = -1;
    private bool _disposed;

    public OsuGradualPerformance(byte[] beatmapText, uint mods)
    {
        unsafe
        {
            NativeRuleset ruleset;
            OsuNativeCalls.Check(OsuNativeCalls.Ruleset_CreateFromId(0 /* Osu */, &ruleset));
            _rulesetHandle = ruleset.Handle;

            try
            {
                var beatmap = NativeBeatmap.Create(beatmapText);
                _beatmapHandle = beatmap.Handle;

                uint difficultyHandle;
                OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_Create(
                    _rulesetHandle, _beatmapHandle, &difficultyHandle));
                _difficultyHandle = difficultyHandle;

                uint performanceHandle;
                OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Create(&performanceHandle));
                _performanceHandle = performanceHandle;

                _modsHandle = OsuNativeMods.CreateModsCollection(mods);

                _timedAttributes = CalculateTimed(_modsHandle);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_performanceHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Destroy(_performanceHandle));
        if (_difficultyHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_Destroy(_difficultyHandle));
        if (_modsHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.ModsCollection_Destroy(_modsHandle));
        if (_beatmapHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.Beatmap_Destroy(_beatmapHandle));
        if (_rulesetHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.Ruleset_Destroy(_rulesetHandle));
    }

    /// <summary> Advances to the object at <paramref name="currentTime"/> and returns its performance, or false if none yet. </summary>
    public bool AdvanceAtTime(int currentTime, PerformanceScore score, out double pp)
    {
        if (_disposed || _timedAttributes.Length == 0)
        {
            pp = 0;
            return false;
        }

        var index = FindIndexAtTime(currentTime);
        if (index < 0 || index == _lastIndex)
        {
            pp = 0;
            return false;
        }

        return CalculateAt(index, score, out pp);
    }

    private bool CalculateAt(int index, PerformanceScore score, out double pp)
    {
        var nativeScore = CreateScoreInfo(score);

        NativeOsuPerformanceAttributes attributes;
        unsafe
        {
            OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Calculate(
                _performanceHandle, nativeScore, _timedAttributes[index].Attributes, &attributes));
        }

        _lastIndex = index;
        pp = attributes.Total;
        return true;
    }

    private NativeScoreInfo CreateScoreInfo(PerformanceScore score) => new()
    {
        RulesetHandle = _rulesetHandle,
        BeatmapHandle = _beatmapHandle,
        ModsHandle = _modsHandle,
        MaxCombo = ToInt32(score.MaxCombo),
        Accuracy = score.Accuracy,
        LegacyTotalScore = NativeNullableInt64.FromNullable(score.LegacyTotalScore),
        CountMiss = ToInt32(score.CountMiss),
        CountMeh = ToInt32(score.Count50),
        CountOk = ToInt32(score.Count100),
        CountGood = ToInt32(score.CountKatu),
        CountGreat = ToInt32(score.Count300),
        CountPerfect = ToInt32(score.CountGeki),
        CountSmallTickMiss = ToInt32(score.CountSmallTickMiss),
        CountSmallTickHit = ToInt32(score.CountSmallTickHit),
        CountLargeTickMiss = ToInt32(score.CountLargeTickMiss),
        CountLargeTickHit = ToInt32(score.CountLargeTickHit),
        CountSliderTailHit = ToInt32(score.CountSliderTailHit),
    };

    private int FindIndexAtTime(int currentTime)
    {
        var lo = 0;
        var hi = _timedAttributes.Length - 1;
        var result = -1;

        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (_timedAttributes[mid].Time <= currentTime)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    private NativeTimedOsuDifficultyAttributes[] CalculateTimed(uint modsCollectionHandle)
    {
        unsafe
        {
            var size = 0;
            var err = OsuNativeCalls.OsuDifficultyCalculator_CalculateTimed(_difficultyHandle, modsCollectionHandle, null, &size);

            if (err != OsuNativeCalls.ErrorCode.BufferSizeQuery && err != OsuNativeCalls.ErrorCode.Success)
                OsuNativeCalls.Check(err);

            if (size <= 0)
                return new NativeTimedOsuDifficultyAttributes[0];

            var attributes = new NativeTimedOsuDifficultyAttributes[size];
            var capacity = attributes.Length;

            fixed (NativeTimedOsuDifficultyAttributes* pAttrs = attributes)
                OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_CalculateTimed(
                    _difficultyHandle, modsCollectionHandle, pAttrs, &capacity));

            if (capacity != attributes.Length && capacity >= 0 && capacity < attributes.Length)
                Array.Resize(ref attributes, capacity);

            return attributes;
        }
    }

    private static int ToInt32(uint value) => value > int.MaxValue ? int.MaxValue : (int)value;
}