using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Osu.Stubs.GameModes.Play.Rulesets;
using Osu.Utils.IL;
using Osu.Utils.Lazy;
using static System.Reflection.Emit.OpCodes;

namespace Osu.Stubs.GameplayElements.HitObjects;

[PublicAPI]
public static class HitObject
{
    /// <summary>
    ///     Original: <c>osu.GameplayElements.HitObjects.HitObject</c>
    ///     Resolved from the fourth parameter type of <c>Ruleset::OnIncreaseScoreHit(...)</c>.
    /// </summary>
    [Stub]
    public static readonly LazyType Class = new(
        "osu.GameplayElements.HitObjects.HitObject",
        () => Ruleset.OnIncreaseScoreHit.Reference.GetParameters()[3].ParameterType
    );

    /// <summary>
    ///     Original: <c>StartTime</c>
    ///     Located from the <c>StartTime == other.StartTime</c> pattern in <c>CompareTo()</c>.
    /// </summary>
    [Stub]
    public static readonly LazyField<int> StartTime = new(
        "osu.GameplayElements.HitObjects.HitObject::StartTime",
        () => FindStartTimeField()
    );

    private static FieldInfo FindStartTimeField()
    {
        foreach (var method in Class.Reference
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(method => method.GetParameters().Length == 1 &&
                                      method.GetParameters()[0].ParameterType == Class.Reference))
        {
            var instructions = MethodReader.GetInstructions(method).ToArray();

            for (var i = 0; i + 3 < instructions.Length; i++)
            {
                if (instructions[i].Opcode != Ldarg_0 ||
                    instructions[i + 1].Opcode != Ldfld ||
                    instructions[i + 2].Opcode != Ldarg_1 ||
                    instructions[i + 3].Opcode != Ldfld)
                    continue;

                var first = (FieldInfo)instructions[i + 1].Operand!;
                var second = (FieldInfo)instructions[i + 3].Operand!;

                if (first == second && first.FieldType == typeof(int))
                    return first;
            }
        }

        throw new InvalidOperationException("Failed to locate the StartTime field of the HitObject class");
    }
}