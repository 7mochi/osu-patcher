using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using Osu.Performance;
using Osu.Stubs.GameModes.Play.Rulesets;
using Osu.Stubs.GameplayElements.HitObjects;
using Osu.Stubs.GameplayElements.Scoring;
using Osu.Utils;
using Osu.Utils.IL;

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
        => HandleScoreHit(__instance, h);

    internal static void HandleScoreHit(object instance, object hitObject)
    {
        if (!PerformanceOptions.ShowPerformanceInGame.Value)
            return;

        if (!PerformanceCalculator.IsInitialized)
        {
            Debug.Fail("OnIncreaseScoreHit called before performance calculator initialized!");
            return;
        }

        var currentScore = Ruleset.CurrentScore.Get(instance);
        if (currentScore == null)
            return;

        var performanceScore = CreateScore(currentScore);

        var timeMs = HitObject.StartTime.Get(hitObject);
        Task.Run(() => PerformanceCalculator.Calculator?.AddScoreUpdate(timeMs, performanceScore));
    }

    private static PerformanceScore CreateScore(object currentScore)
    {
        var count50 = ToUInt32(Score.Count50.Get(currentScore));
        var count100 = ToUInt32(Score.Count100.Get(currentScore));
        var count300 = ToUInt32(Score.Count300.Get(currentScore));
        var countMiss = ToUInt32(Score.CountMiss.Get(currentScore));
        var countKatu = ToUInt32(Score.CountKatu.Get(currentScore));
        var countGeki = ToUInt32(Score.CountGeki.Get(currentScore));

        return new PerformanceScore
        {
            MaxCombo = ToUInt32(Score.MaxCombo.Get(currentScore)),
            PassedObjects = count300 + count100 + count50 + countMiss,
            Count300 = count300,
            Count100 = count100,
            Count50 = count50,
            CountMiss = countMiss,
            CountKatu = countKatu,
            CountGeki = countGeki,
            Accuracy = Score.GetAccuracy.Invoke<float>(currentScore),
        };
    }

    private static uint ToUInt32(object? value) => Convert.ToUInt32(value ?? 0, System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
///     Hooks ruleset overrides that do not call <c>base.OnIncreaseScoreHit</c> (currently catch).
/// </summary>
[OsuPatch]
[HarmonyPatch]
[UsedImplicitly]
internal static class TrackOnNonBaseScoreHit
{
    [UsedImplicitly]
    [HarmonyTargetMethod]
    private static MethodBase Target() => FindTarget();

    [UsedImplicitly]
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void After(
        object __instance, // is Ruleset
        [HarmonyArgument(3)] object h) // is HitObject
        => TrackOnScoreHit.HandleScoreHit(__instance, h);

    private static MethodBase FindTarget()
    {
        var baseMethod = Ruleset.OnIncreaseScoreHit.Reference;
        var baseParameters = baseMethod.GetParameters();

        foreach (var type in OsuAssembly.Types)
        foreach (var method in type.GetMethods(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (method == baseMethod || method.Name != baseMethod.Name || !HasSameSignature(method, baseParameters))
                continue;

            var body = method.GetMethodBody();
            if (body == null)
                continue;

            var callsBase = MethodReader.GetInstructions(method)
                .Any(instruction => instruction.Opcode == System.Reflection.Emit.OpCodes.Call &&
                                   instruction.Operand is MethodBase called &&
                                   called == baseMethod);

            if (!callsBase)
                return method;
        }

        throw new InvalidOperationException("Failed to locate a non-base OnIncreaseScoreHit override");
    }

    private static bool HasSameSignature(MethodBase method, IReadOnlyList<ParameterInfo> baseParameters)
    {
        var parameters = method.GetParameters();
        return parameters.Length == baseParameters.Count &&
               parameters.Select(parameter => parameter.ParameterType)
                   .SequenceEqual(baseParameters.Select(parameter => parameter.ParameterType));
    }
}
