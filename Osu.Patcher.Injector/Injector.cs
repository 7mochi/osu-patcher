using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using HoLLy.ManagedInjector;

namespace Osu.Patcher.Injector;

[SupportedOSPlatform("windows")]
internal static class Injector
{
    public static void Main(string[] args)
    {
        try
        {
            uint? explicitPid = args.Length > 0 && uint.TryParse(args[0], out var pid) ? pid : null;

            using var proc = new InjectableProcess(GetOsuPid(explicitPid));
            var dllPath = Path.GetFullPath(typeof(Injector).Assembly.Location + @"\..\osu!.hook.dll");

            proc.Inject(dllPath, "Osu.Patcher.Hook.Hook", "Initialize");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);

            Console.WriteLine("\nPress any key to continue...");
            Console.Write("\a"); // Bell sound
            Console.ReadKey();
        }
    }

    /// <summary>
    ///     Finds a <c>osu!.exe</c> process to inject into, verifying it was launched with a
    ///     <c>-devserver</c> argument (and not Bancho). <paramref name="explicitPid"/> overrides
    ///     the automatic search. WMI is not used, so this also works when osu! runs under Wine.
    /// </summary>
    private static uint GetOsuPid(uint? explicitPid)
    {
        if (explicitPid.HasValue)
            return ValidateOsuProcess(Process.GetProcessById((int)explicitPid.Value), explicitPid.Value);

        foreach (var process in Process.GetProcessesByName("osu!"))
        {
            using (process)
            {
                if (TryGetCommandLine(process, out var cmdline) && IsSafeToInject(cmdline))
                    return (uint)process.Id;
            }
        }

        throw new Exception("Cannot find a running osu! process (launch it with -devserver)!");
    }

    private static uint ValidateOsuProcess(Process process, uint pid)
    {
        using (process)
        {
            if (process.ProcessName != "osu!")
                throw new Exception($"Process with PID {pid} is not osu!.");

            if (!TryGetCommandLine(process, out var cmdline))
                throw new Exception($"Cannot read the command line of PID {pid}.");

            if (!IsSafeToInject(cmdline))
                throw new Exception("Will not inject into osu! connected to Bancho!");

            return pid;
        }
    }

    private static bool IsSafeToInject(string cmdline) =>
        cmdline.Contains("-devserver") && !cmdline.Contains("ppy.sh");

    /// <summary>
    ///     Reads the process command line without WMI. Uses NtQueryInformationProcess on Windows 8+,
    ///     with a PEB fallback that Wine supports (class 60 is unimplemented there).
    /// </summary>
    private static bool TryGetCommandLine(Process process, out string result)
    {
        result = "";

        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, false, process.Id);
        if (handle == IntPtr.Zero)
            return false;

        try
        {
            var status = NtQueryInformationProcess(handle, ProcessCommandLineInformation, IntPtr.Zero, 0, out var length);
            if (status == STATUS_INFO_LENGTH_MISMATCH)
            {
                var buffer = Marshal.AllocHGlobal(length);
                try
                {
                    status = NtQueryInformationProcess(handle, ProcessCommandLineInformation, buffer, length, out _);
                    if (status == 0)
                    {
                        result = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, IntPtr.Size), (ushort)Marshal.ReadInt16(buffer) / 2) ?? "";
                        return true;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return TryGetCommandLineFromPeb(handle, out result);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    ///     Reads the command line from the target's PEB, handling both 32-bit (WOW64, like osu!)
    ///     and 64-bit processes.
    /// </summary>
    private static bool TryGetCommandLineFromPeb(IntPtr hProcess, out string result)
    {
        result = "";

        // A non-zero WOW64 PEB address is returned for 32-bit processes.
        var status = NtQueryInformationProcess(hProcess, ProcessWow64Information, out ulong peb32, Marshal.SizeOf<ulong>(), out _);
        if (status == 0 && peb32 != 0)
        {
            var peb = new IntPtr(unchecked((long)peb32));
            var procParams = ReadPointer(hProcess, peb, 0x10, is32Bit: true);
            return procParams != IntPtr.Zero &&
                   ReadUnicodeString(hProcess, procParams + 0x40, is32Bit: true, out result);
        }

        var pbi = new PROCESS_BASIC_INFORMATION();
        if (NtQueryInformationProcess(hProcess, ProcessBasicInformation, ref pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out _) != 0)
            return false;

        var procParams64 = ReadPointer(hProcess, pbi.PebBaseAddress, 0x20, is32Bit: false);
        return procParams64 != IntPtr.Zero &&
               ReadUnicodeString(hProcess, procParams64 + 0x70, is32Bit: false, out result);
    }

    private static IntPtr ReadPointer(IntPtr hProcess, IntPtr address, int offset, bool is32Bit)
    {
        var buffer = new byte[is32Bit ? 4 : IntPtr.Size];
        if (!ReadProcessMemory(hProcess, address + offset, buffer, buffer.Length, out _))
            return IntPtr.Zero;

        return is32Bit
            ? new IntPtr(BitConverter.ToUInt32(buffer, 0))
            : new IntPtr(BitConverter.ToInt64(buffer, 0));
    }

    private static bool ReadUnicodeString(IntPtr hProcess, IntPtr address, bool is32Bit, out string result)
    {
        result = "";

        var header = new byte[is32Bit ? 8 : 16];
        if (!ReadProcessMemory(hProcess, address, header, header.Length, out _))
            return false;

        ushort length = BitConverter.ToUInt16(header, 0);
        var buffer = is32Bit
            ? new IntPtr(BitConverter.ToUInt32(header, 4))
            : new IntPtr(BitConverter.ToInt64(header, 8));
        if (length == 0 || buffer == IntPtr.Zero)
            return false;

        var data = new byte[length];
        if (!ReadProcessMemory(hProcess, buffer, data, data.Length, out _))
            return false;

        result = Encoding.Unicode.GetString(data);
        return true;
    }

    private const int ProcessCommandLineInformation = 60;
    private const int ProcessWow64Information = 26;
    private const int ProcessBasicInformation = 0;
    private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const int PROCESS_VM_READ = 0x0010;
    private const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, IntPtr processInformation, int processInformationLength, out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, out ulong processInformation, int processInformationLength, out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(int access, bool inheritHandle, int pid);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddress, byte[] buffer, int size, out int read);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniquePid;
        public IntPtr Reserved3;
    }
}
