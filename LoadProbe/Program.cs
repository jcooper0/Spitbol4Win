// LoadProbe -- independent check of the exact Win32 calls SPITBOL's loadDll
// makes (LoadLibraryA + GetProcAddress), against the same DLL path and symbol
// names used by SpitbolTests/cases/.../ExternalFnErrors.sbl.
//
// Purpose: when that test reports &errtype=22 ("undefined function called"),
// the real cause is that load() failed (err 142) and the test's setexit trap
// swallowed it, leaving efint/efstr/efreal/effile undefined.  err 142 comes
// from loadDll returning -1, i.e. LoadLibraryA or GetProcAddress failing.
// This probe reproduces those two calls in isolation:
//   * If this probe SUCCEEDS for all four names, the DLL and environment are
//     fine -- the fault is in how SPITBOL passes the path/name into loadDll
//     (run the build with SPITBOL_LOADDLL_DEBUG=1 to see the exact strings).
//   * If this probe FAILS, the DLL/environment is the problem (wrong arch,
//     missing CRT dependency, bad path, unexported symbol) and the Win32
//     error code below says which.
//
// Build/run (no project file needed):
//     cd LoadProbe && dotnet run -- "<path-to-eflib.dll>"
// If no path is given, the default fixture path is used.

using System;
using System.Runtime.InteropServices;

internal static class Program
{
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryA(string lpLibFileName);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern int FreeLibrary(IntPtr hModule);

    private static int Main(string[] args)
    {
        string dll = args.Length > 0
            ? args[0]
            : @"C:\Users\jcooper\source\repos\Spitbol4Win\SpitbolTests\fixtures\eflib.dll";

        string[] names = { "efint", "efstr", "efreal", "effile" };

        Console.WriteLine($"Process is {(Environment.Is64BitProcess ? "x64" : "x86")}");
        Console.WriteLine($"Probing: {dll}");
        Console.WriteLine($"Exists on disk: {System.IO.File.Exists(dll)}");
        Console.WriteLine();

        IntPtr h = LoadLibraryA(dll);
        if (h == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Console.WriteLine($"LoadLibraryA FAILED. Win32 error {err}: {new System.ComponentModel.Win32Exception(err).Message}");
            Console.WriteLine("=> The DLL itself could not be loaded (path, architecture, or a");
            Console.WriteLine("   missing dependency such as the VC++ runtime). This is the same");
            Console.WriteLine("   condition that makes SPITBOL's load() raise error 142.");
            return 1;
        }

        Console.WriteLine($"LoadLibraryA OK (handle=0x{h.ToInt64():X})");

        int failures = 0;
        foreach (string n in names)
        {
            IntPtr p = GetProcAddress(h, n);
            if (p == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Console.WriteLine($"  GetProcAddress(\"{n}\") FAILED. Win32 error {err}");
                failures++;
            }
            else
            {
                Console.WriteLine($"  GetProcAddress(\"{n}\") OK (0x{p.ToInt64():X})");
            }
        }

        FreeLibrary(h);
        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("RESULT: DLL loads and all four symbols resolve.");
            Console.WriteLine("=> The DLL/environment is fine. If SPITBOL still raises 142, the");
            Console.WriteLine("   problem is in the path/name SPITBOL hands to loadDll. Rebuild and");
            Console.WriteLine("   run the failing case with SPITBOL_LOADDLL_DEBUG=1 set to print the");
            Console.WriteLine("   exact dllName/fcnName and GetLastError from inside loadDll.");
            return 0;
        }

        Console.WriteLine($"RESULT: {failures} symbol(s) failed to resolve.");
        Console.WriteLine("=> eflib.dll does not export those names as probed (check the build:");
        Console.WriteLine("   `dumpbin /exports eflib.dll`). This is what makes load() raise 142.");
        return 2;
    }
}
