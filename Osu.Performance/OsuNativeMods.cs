using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Osu.Performance;

/// <summary> Maps legacy osu! Stable mod flags to lazer mod acronyms and builds the native mods collection. </summary>
internal static class OsuNativeMods
{
    private const uint NoFail = 1 << 0;
    private const uint Easy = 1 << 1;
    private const uint TouchDevice = 1 << 2;
    private const uint Hidden = 1 << 3;
    private const uint HardRock = 1 << 4;
    private const uint SuddenDeath = 1 << 5;
    private const uint DoubleTime = 1 << 6;
    private const uint Relax = 1 << 7;
    private const uint HalfTime = 1 << 8;
    private const uint Nightcore = 1 << 9;
    private const uint Flashlight = 1 << 10;
    private const uint Autoplay = 1 << 11;
    private const uint SpunOut = 1 << 12;
    private const uint Relax2 = 1 << 13;
    private const uint Perfect = 1 << 14;

    /// <summary> Converts the legacy mod flags to a deduplicated list of acronyms (NC over DT, PF over SD). </summary>
    public static string[] ToAcronyms(uint mods)
    {
        var acronyms = new List<string>();

        Add(NoFail, "NF");
        Add(Easy, "EZ");
        Add(TouchDevice, "TD");
        Add(Hidden, "HD");
        Add(HardRock, "HR");
        Add(Flashlight, "FL");
        Add(HalfTime, "HT");
        Add(Autoplay, "AT");
        Add(SpunOut, "SO");
        Add(Relax, "RX");
        Add(Relax2, "AP");

        if ((mods & Nightcore) != 0)
            Add(Nightcore, "NC");
        else
            Add(DoubleTime, "DT");

        if ((mods & Perfect) != 0)
            Add(Perfect, "PF");
        else
            Add(SuddenDeath, "SD");

        return acronyms.ToArray();

        void Add(uint flag, string acronym)
        {
            if ((mods & flag) != 0)
                acronyms.Add(acronym);
        }
    }

    /// <summary> Creates the native mods collection for the given mods and returns its handle. </summary>
    public static uint CreateModsCollection(uint mods)
    {
        unsafe
        {
            NativeModsCollection collection;
            OsuNativeCalls.Check(OsuNativeCalls.ModsCollection_Create(&collection));

            try
            {
                var acronyms = ToAcronyms(mods).ToList();

                if (!acronyms.Contains("CL"))
                    acronyms.Add("CL");

                foreach (var acronym in acronyms)
                {
                    uint modHandle = CreateMod(acronym);
                    try
                    {
                        OsuNativeCalls.Check(OsuNativeCalls.ModsCollection_Add(collection.Handle, modHandle));
                    }
                    finally
                    {
                        OsuNativeCalls.Check(OsuNativeCalls.Mod_Destroy(modHandle));
                    }
                }

                return collection.Handle;
            }
            catch
            {
                OsuNativeCalls.Check(OsuNativeCalls.ModsCollection_Destroy(collection.Handle));
                throw;
            }
        }
    }

    private static unsafe uint CreateMod(string acronym)
    {
        var bytes = Encoding.UTF8.GetBytes(acronym + '\0');

        uint handle;
        fixed (byte* acronymPtr = bytes)
            OsuNativeCalls.Check(OsuNativeCalls.Mod_Create(acronymPtr, &handle));

        return handle;
    }
}