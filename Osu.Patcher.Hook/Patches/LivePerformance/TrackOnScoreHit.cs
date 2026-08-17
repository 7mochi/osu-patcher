using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using Osu.Performance;
using Osu.Stubs.GameModes.Play.Rulesets;
using Osu.Stubs.GameplayElements.HitObjects;
using Osu.Stubs.GameplayElements.Scoring;

namespace Osu.Patcher.Hook.Patches.LivePerformance;

/// <summary>
///     Hooks <c>Ruleset::OnIncreaseScoreHit(...)</c> to send score updates to our performance calculator
///     so it can recalculate performance based on new HitObject judgements.
/// </summary>
[OsuPatch]
[HarmonyPatch]
[UsedImplicitly]
internal static class TrackOnScoreHit
{
    [UsedImplicitly]
    [HarmonyTargetMethod]
    private static MethodBase Target() => Ruleset.OnIncreaseScoreHit.Reference;

    [UsedImplicitly]
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void After(
        object __instance, // is Ruleset
        [HarmonyArgument(3)] object h) // is HitObject
    {
        if (!PerformanceOptions.ShowPerformanceInGame.Value)
            return;

        if (!PerformanceCalculator.IsInitialized)
        {
            Debug.Fail("OnIncreaseScoreHit called before performance calculator initialized!");
            return;
        }

        var CurrentScore = Ruleset.CurrentScore.Get(__instance);
        if (CurrentScore == null)
            return;

        var performanceScore = CreateScore(CurrentScore);

        var timeMs = HitObject.StartTime.Get(h);
        Task.Run(() => PerformanceCalculator.Calculator?.AddScoreUpdate(timeMs, performanceScore));
    }

    private static PerformanceScore CreateScore(object currentScore)
    {
        var count50 = ToUInt32(Score.Count50.Get(currentScore));
        var count100 = ToUInt32(Score.Count100.Get(currentScore));
        var count300 = ToUInt32(Score.Count300.Get(currentScore));
        var countMiss = ToUInt32(Score.CountMiss.Get(currentScore));

        return new PerformanceScore
        {
            MaxCombo = ToUInt32(Score.MaxCombo.Get(currentScore)),
            PassedObjects = count300 + count100 + count50 + countMiss,
            Count300 = count300,
            Count100 = count100,
            Count50 = count50,
            CountMiss = countMiss,
            Accuracy = Score.GetAccuracy.Invoke<float>(currentScore),
        };
    }

    private static uint ToUInt32(object? value) => Convert.ToUInt32(value ?? 0, System.Globalization.CultureInfo.InvariantCulture);
}