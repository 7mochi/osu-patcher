using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Osu.Performance;

/// <summary> P/Invoke surface of the native <c>osu.Native.dll</c> (osu!'s difficulty/performance library). </summary>
public static class OsuNativeCalls
{
    private const string DllName = "osu.Native.dll";

    private static IntPtr _loadedHandle = IntPtr.Zero;
    private static readonly object LoadLock = new();

    /// <summary> Loads <c>osu.Native.dll</c> from this assembly's directory (not on the osu! process's search path). </summary>
    public static void EnsureLoaded()
    {
        if (_loadedHandle != IntPtr.Zero)
            return;

        lock (LoadLock)
        {
            if (_loadedHandle != IntPtr.Zero)
                return;

            var hookDir = Path.GetDirectoryName(typeof(OsuNativeCalls).Assembly.Location) ?? ".";
            _loadedHandle = LoadLibrary(Path.Combine(hookDir, DllName));
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode Ruleset_CreateFromId(int rulesetId, NativeRuleset* ruleset);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode Beatmap_CreateFromText(byte* text, NativeBeatmap* beatmap);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode Beatmap_Destroy(uint beatmapHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode Ruleset_Destroy(uint rulesetHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode OsuDifficultyCalculator_Create(uint ruleset, uint beatmap, uint* calculator);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode OsuDifficultyCalculator_CalculateTimed(
        uint calc, uint modsCollectionHandle, NativeTimedOsuDifficultyAttributes* attributes, int* bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode OsuDifficultyCalculator_Destroy(uint handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode OsuPerformanceCalculator_Create(uint* calculator);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode OsuPerformanceCalculator_Calculate(
        uint calc, NativeScoreInfo score, NativeOsuDifficultyAttributes difficulty, NativeOsuPerformanceAttributes* attributes);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode OsuPerformanceCalculator_Destroy(uint handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode ModsCollection_Create(NativeModsCollection* modCollection);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode ModsCollection_Add(uint modsCollectionHandle, uint modHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode ModsCollection_Destroy(uint handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern unsafe ErrorCode Mod_Create(byte* acronym, uint* modHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ErrorCode Mod_Destroy(uint modHandle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern string ErrorHandler_GetLastMessage();

    internal enum ErrorCode : sbyte
    {
        EndOfEnumeration = -2,
        BufferSizeQuery = -1,
        Success = 0,
        ObjectNotResolved = 1,
        RulesetUnavailable = 2,
        UnexpectedRuleset = 3,
        Failure = 127
    }

    internal static void Check(ErrorCode err)
    {
        if (err == ErrorCode.Success)
            return;

        var message = ErrorHandler_GetLastMessage();
        throw new Exception($"An error has occurred when calling osu-native ({err}). Message: {message}");
    }
}