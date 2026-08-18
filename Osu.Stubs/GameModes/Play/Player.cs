using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Osu.Stubs.GameplayElements.Scoring;
using Osu.Utils.IL;
using Osu.Utils.Lazy;
using static System.Reflection.Emit.OpCodes;

namespace Osu.Stubs.GameModes.Play;

[PublicAPI]
public static class Player
{
    /// <summary>
    ///     Original: <c>osu.GameModes.Play.Player</c>
    ///     b20240124: <c>#=zOTWUr4vq60U15SRmD_JItyatbhdR</c>
    /// </summary>
    [Stub]
    public static readonly LazyType Class = new(
        "osu.GameModes.Play.Player",
        () => GetAllowDoubleSkip!.Reference.DeclaringType!
    );

    /// <summary>
    ///     Original: <c>get_AllowDoubleSkip()</c> (property getter)
    ///     b20240124: <c>#=zp29IlAJ43g4WRArPQA==</c>
    /// </summary>
    [Stub]
    public static readonly LazyMethod GetAllowDoubleSkip = LazyMethod.ByPartialSignature(
        "osu.GameModes.Play.Player::get_AllowDoubleSkip()",
        [
            Neg,
            Stloc_0,
            Ldarg_0,
            Isinst,
            Brtrue_S,
            Ldsfld,
            Ldloc_0,
            Call,
            Brtrue_S,
            Ldc_I4_0,
            Br_S,
        ]
    );

    /// <summary>
    ///     Original: <c>OnLoadComplete(bool success)</c>
    ///     b20240124: <c>#=zXb_K4cZvV$uy</c>
    /// </summary>
    [Stub]
    public static readonly LazyMethod<bool> OnLoadComplete = LazyMethod<bool>.ByPartialSignature(
        "osu.GameModes.Play.Player::OnLoadComplete(bool)",
        [
            Br,
            Ldloc_S,
            Callvirt,
            Unbox_Any,
            Stloc_2,
            Ldsfld,
            Ldfld,
            Call,
        ]
    );

    /// <summary>
    ///     Original: <c>currentScore</c>
    ///     b20240124: <c>#=zF6h5l4j0$TfX</c>
    /// </summary>
    [Stub]
    public static readonly LazyField<object?> CurrentScore = new(
        "osu.GameModes.Play.Player::currentScore",
        () => Class.Reference
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(field => field.FieldType == Score.Class.Reference)
    );

    /// <summary>
    ///     Original: <c>get_Mode()</c> (the mode currently being played)
    /// </summary>
    [Stub]
    public static readonly LazyMethod<object> GetMode = new(
        "osu.GameModes.Play.Player::get_Mode()",
        FindGetMode
    );

    private static MethodInfo FindGetMode()
    {
        foreach (var method in Class.Reference.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!method.ReturnType.IsEnum || method.GetParameters().Length != 0)
                continue;

            var instructions = MethodReader.GetInstructions(method).ToArray();
            if (instructions.Length != 2 || instructions[0].Opcode != Ldsfld || instructions[1].Opcode != Ret)
                continue;

            if (instructions[0].Operand is FieldInfo { IsStatic: true } field &&
                field.DeclaringType == method.DeclaringType &&
                field.FieldType == method.ReturnType)
                return method;
        }

        throw new InvalidOperationException("Failed to locate Player.get_Mode");
    }
}