using System;
using System.Linq;

namespace Osu.Performance;

/// <summary>
///     A gradual osu! performance calculator: pre-computes the per-object difficulty attributes
///     once, then recalculates performance for the current score as the playhead advances.
/// </summary>
internal sealed class OsuGradualPerformance : IDisposable
{
    private readonly int _mode;
    private readonly uint _rulesetHandle;
    private readonly uint _modsHandle;
    private readonly uint _beatmapHandle;
    private readonly uint _difficultyHandle;
    private readonly uint _performanceHandle;

    private NativeTimedOsuDifficultyAttributes[] _osuAttrs = [];
    private NativeTimedTaikoDifficultyAttributes[] _taikoAttrs = [];
    private NativeTimedCatchDifficultyAttributes[] _catchAttrs = [];
    private NativeTimedManiaDifficultyAttributes[] _maniaAttrs = [];
    private double[] _times = [];
    private int _lastIndex = -1;
    private bool _disposed;

    public OsuGradualPerformance(int mode, byte[] beatmapText, uint mods)
    {
        _mode = mode;

        unsafe
        {
            NativeRuleset ruleset;
            OsuNativeCalls.Check(OsuNativeCalls.Ruleset_CreateFromId(mode, &ruleset));
            _rulesetHandle = ruleset.Handle;

            try
            {
                var beatmap = NativeBeatmap.Create(beatmapText);
                _beatmapHandle = beatmap.Handle;

                uint difficultyHandle;
                uint performanceHandle;
                switch (mode)
                {
                    case 0:
                        OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_Create(_rulesetHandle, _beatmapHandle, &difficultyHandle));
                        OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Create(&performanceHandle));
                        break;
                    case 1:
                        OsuNativeCalls.Check(OsuNativeCalls.TaikoDifficultyCalculator_Create(_rulesetHandle, _beatmapHandle, &difficultyHandle));
                        OsuNativeCalls.Check(OsuNativeCalls.TaikoPerformanceCalculator_Create(&performanceHandle));
                        break;
                    case 2:
                        OsuNativeCalls.Check(OsuNativeCalls.CatchDifficultyCalculator_Create(_rulesetHandle, _beatmapHandle, &difficultyHandle));
                        OsuNativeCalls.Check(OsuNativeCalls.CatchPerformanceCalculator_Create(&performanceHandle));
                        break;
                    case 3:
                        OsuNativeCalls.Check(OsuNativeCalls.ManiaDifficultyCalculator_Create(_rulesetHandle, _beatmapHandle, &difficultyHandle));
                        OsuNativeCalls.Check(OsuNativeCalls.ManiaPerformanceCalculator_Create(&performanceHandle));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode));
                }

                _difficultyHandle = difficultyHandle;
                _performanceHandle = performanceHandle;

                _modsHandle = OsuNativeMods.CreateModsCollection(mods);

                CalculateTimed();
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
            DestroyPerformance(_performanceHandle);
        if (_difficultyHandle != 0)
            DestroyDifficulty(_difficultyHandle);
        if (_modsHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.ModsCollection_Destroy(_modsHandle));
        if (_beatmapHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.Beatmap_Destroy(_beatmapHandle));
        if (_rulesetHandle != 0)
            OsuNativeCalls.Check(OsuNativeCalls.Ruleset_Destroy(_rulesetHandle));
    }

    private void DestroyDifficulty(uint handle)
    {
        switch (_mode)
        {
            case 1: OsuNativeCalls.Check(OsuNativeCalls.TaikoDifficultyCalculator_Destroy(handle)); break;
            case 2: OsuNativeCalls.Check(OsuNativeCalls.CatchDifficultyCalculator_Destroy(handle)); break;
            case 3: OsuNativeCalls.Check(OsuNativeCalls.ManiaDifficultyCalculator_Destroy(handle)); break;
            default: OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_Destroy(handle)); break;
        }
    }

    private void DestroyPerformance(uint handle)
    {
        switch (_mode)
        {
            case 1: OsuNativeCalls.Check(OsuNativeCalls.TaikoPerformanceCalculator_Destroy(handle)); break;
            case 2: OsuNativeCalls.Check(OsuNativeCalls.CatchPerformanceCalculator_Destroy(handle)); break;
            case 3: OsuNativeCalls.Check(OsuNativeCalls.ManiaPerformanceCalculator_Destroy(handle)); break;
            default: OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Destroy(handle)); break;
        }
    }

    /// <summary> Advances to the object at <paramref name="currentTime"/> and returns its performance, or false if none yet. </summary>
    public bool AdvanceAtTime(int currentTime, PerformanceScore score, out double pp)
    {
        if (_disposed || _times.Length == 0)
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

        unsafe
        {
            switch (_mode)
            {
                case 0:
                {
                    NativeOsuPerformanceAttributes attributes;
                    OsuNativeCalls.Check(OsuNativeCalls.OsuPerformanceCalculator_Calculate(
                        _performanceHandle, nativeScore, _osuAttrs[index].Attributes, &attributes));
                    pp = attributes.Total;
                    break;
                }
                case 1:
                {
                    NativeTaikoPerformanceAttributes attributes;
                    OsuNativeCalls.Check(OsuNativeCalls.TaikoPerformanceCalculator_Calculate(
                        _performanceHandle, nativeScore, _taikoAttrs[index].Attributes, &attributes));
                    pp = attributes.Total;
                    break;
                }
                case 2:
                {
                    NativeCatchPerformanceAttributes attributes;
                    OsuNativeCalls.Check(OsuNativeCalls.CatchPerformanceCalculator_Calculate(
                        _performanceHandle, nativeScore, _catchAttrs[index].Attributes, &attributes));
                    pp = attributes.Total;
                    break;
                }
                case 3:
                {
                    NativeManiaPerformanceAttributes attributes;
                    OsuNativeCalls.Check(OsuNativeCalls.ManiaPerformanceCalculator_Calculate(
                        _performanceHandle, nativeScore, _maniaAttrs[index].Attributes, &attributes));
                    pp = attributes.Total;
                    break;
                }
                default:
                    pp = 0;
                    return false;
            }
        }

        _lastIndex = index;
        return true;
    }

    /// <summary>
    ///     Maps the stable score counts to the native score info fields, per ruleset.
    /// </summary>
    private NativeScoreInfo CreateScoreInfo(PerformanceScore score)
    {
        var info = new NativeScoreInfo
        {
            RulesetHandle = _rulesetHandle,
            BeatmapHandle = _beatmapHandle,
            ModsHandle = _modsHandle,
            MaxCombo = ToInt32(score.MaxCombo),
            Accuracy = score.Accuracy,
            LegacyTotalScore = NativeNullableInt64.FromNullable(score.LegacyTotalScore),
            CountMiss = ToInt32(score.CountMiss),
            CountSliderTailHit = ToInt32(score.CountSliderTailHit),
        };

        switch (_mode)
        {
            case 2:
                info.CountGreat = ToInt32(score.Count300);
                info.CountLargeTickHit = ToInt32(score.Count100);
                info.CountLargeTickMiss = ToInt32(score.CountLargeTickMiss);
                info.CountSmallTickHit = ToInt32(score.Count50);
                info.CountSmallTickMiss = ToInt32(score.CountKatu);
                break;
            case 3:
                info.CountPerfect = ToInt32(score.CountGeki);
                info.CountGreat = ToInt32(score.Count300);
                info.CountGood = ToInt32(score.CountKatu);
                info.CountOk = ToInt32(score.Count100);
                info.CountMeh = ToInt32(score.Count50);
                break;
            default:
                info.CountGreat = ToInt32(score.Count300);
                info.CountOk = ToInt32(score.Count100);
                info.CountMeh = ToInt32(score.Count50);
                break;
        }

        return info;
    }

    private int FindIndexAtTime(int currentTime)
    {
        var lo = 0;
        var hi = _times.Length - 1;
        var result = -1;

        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (_times[mid] <= currentTime)
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

    private void CalculateTimed()
    {
        switch (_mode)
        {
            case 0: _osuAttrs = CalculateTimedOsu(); break;
            case 1: _taikoAttrs = CalculateTimedTaiko(); break;
            case 2: _catchAttrs = CalculateTimedCatch(); break;
            case 3: _maniaAttrs = CalculateTimedMania(); break;
        }

        _times = _mode switch
        {
            0 => _osuAttrs.Select(attr => attr.Time).ToArray(),
            1 => _taikoAttrs.Select(attr => attr.Time).ToArray(),
            2 => _catchAttrs.Select(attr => attr.Time).ToArray(),
            _ => _maniaAttrs.Select(attr => attr.Time).ToArray(),
        };
    }

    private NativeTimedOsuDifficultyAttributes[] CalculateTimedOsu()
    {
        unsafe
        {
            var size = 0;
            var err = OsuNativeCalls.OsuDifficultyCalculator_CalculateTimed(_difficultyHandle, _modsHandle, null, &size);

            if (err != OsuNativeCalls.ErrorCode.BufferSizeQuery && err != OsuNativeCalls.ErrorCode.Success)
                OsuNativeCalls.Check(err);

            if (size <= 0)
                return [];

            var attributes = new NativeTimedOsuDifficultyAttributes[size];
            var capacity = attributes.Length;

            fixed (NativeTimedOsuDifficultyAttributes* pAttrs = attributes)
                OsuNativeCalls.Check(OsuNativeCalls.OsuDifficultyCalculator_CalculateTimed(
                    _difficultyHandle, _modsHandle, pAttrs, &capacity));

            if (capacity != attributes.Length && capacity >= 0 && capacity < attributes.Length)
                Array.Resize(ref attributes, capacity);

            return attributes;
        }
    }

    private NativeTimedTaikoDifficultyAttributes[] CalculateTimedTaiko()
    {
        unsafe
        {
            var size = 0;
            var err = OsuNativeCalls.TaikoDifficultyCalculator_CalculateTimed(_difficultyHandle, _modsHandle, null, &size);

            if (err != OsuNativeCalls.ErrorCode.BufferSizeQuery && err != OsuNativeCalls.ErrorCode.Success)
                OsuNativeCalls.Check(err);

            if (size <= 0)
                return [];

            var attributes = new NativeTimedTaikoDifficultyAttributes[size];
            var capacity = attributes.Length;

            fixed (NativeTimedTaikoDifficultyAttributes* pAttrs = attributes)
                OsuNativeCalls.Check(OsuNativeCalls.TaikoDifficultyCalculator_CalculateTimed(
                    _difficultyHandle, _modsHandle, pAttrs, &capacity));

            if (capacity != attributes.Length && capacity >= 0 && capacity < attributes.Length)
                Array.Resize(ref attributes, capacity);

            return attributes;
        }
    }

    private NativeTimedCatchDifficultyAttributes[] CalculateTimedCatch()
    {
        unsafe
        {
            var size = 0;
            var err = OsuNativeCalls.CatchDifficultyCalculator_CalculateTimed(_difficultyHandle, _modsHandle, null, &size);

            if (err != OsuNativeCalls.ErrorCode.BufferSizeQuery && err != OsuNativeCalls.ErrorCode.Success)
                OsuNativeCalls.Check(err);

            if (size <= 0)
                return [];

            var attributes = new NativeTimedCatchDifficultyAttributes[size];
            var capacity = attributes.Length;

            fixed (NativeTimedCatchDifficultyAttributes* pAttrs = attributes)
                OsuNativeCalls.Check(OsuNativeCalls.CatchDifficultyCalculator_CalculateTimed(
                    _difficultyHandle, _modsHandle, pAttrs, &capacity));

            if (capacity != attributes.Length && capacity >= 0 && capacity < attributes.Length)
                Array.Resize(ref attributes, capacity);

            return attributes;
        }
    }

    private NativeTimedManiaDifficultyAttributes[] CalculateTimedMania()
    {
        unsafe
        {
            var size = 0;
            var err = OsuNativeCalls.ManiaDifficultyCalculator_CalculateTimed(_difficultyHandle, _modsHandle, null, &size);

            if (err != OsuNativeCalls.ErrorCode.BufferSizeQuery && err != OsuNativeCalls.ErrorCode.Success)
                OsuNativeCalls.Check(err);

            if (size <= 0)
                return [];

            var attributes = new NativeTimedManiaDifficultyAttributes[size];
            var capacity = attributes.Length;

            fixed (NativeTimedManiaDifficultyAttributes* pAttrs = attributes)
                OsuNativeCalls.Check(OsuNativeCalls.ManiaDifficultyCalculator_CalculateTimed(
                    _difficultyHandle, _modsHandle, pAttrs, &capacity));

            if (capacity != attributes.Length && capacity >= 0 && capacity < attributes.Length)
                Array.Resize(ref attributes, capacity);

            return attributes;
        }
    }

    private static int ToInt32(uint value) => value > int.MaxValue ? int.MaxValue : (int)value;
}