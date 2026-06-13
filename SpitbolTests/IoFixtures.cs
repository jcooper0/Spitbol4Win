using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpitbolTests
{
    /// <summary>
    /// Prepares the device fixtures that the I/O conformance tests need in
    /// order to elicit the environment-dependent error codes that otherwise
    /// degrade to SKIP:
    ///
    ///   175  REWIND on a non-seekable device   (rewind.sbl)
    ///   296  SET pointer on a non-seekable dev. (set.sbl, .cust builds only)
    ///   207  OUTPUT to an always-full device    (output.sbl)
    ///   094  EJECT to an always-full device     (eject.sbl, full-device variant)
    ///
    /// Each test reads a device path from a small ".cfg" file in its working
    /// directory; if the file is absent the test records a SKIP instead of a
    /// FAIL. All four assertions use the non-strict CHK form (strict = 0), so
    /// on a platform where the device does not raise the expected code the
    /// test still degrades gracefully to SKIP rather than failing.
    ///
    /// Usage from an xUnit test or a runner, before launching Spitbol.exe:
    ///
    ///     using (IoFixtures.Prepare(workDir))
    ///     {
    ///         var r = RunSbl(workDir, "rewind.sbl");
    ///         Assert.DoesNotContain("*FAIL", r.Stdout);
    ///     }   // devices torn down here, after the interpreter has exited
    ///
    /// The returned IDisposable MUST stay alive for the duration of the
    /// interpreter run: on Unix it holds the FIFO open read-write so the
    /// test's read-only open never blocks and never sees end-of-file.
    /// </summary>
    public static class IoFixtures
    {
        public const string DevCfgName  = "io_t_dev.cfg";   // non-seekable device
        public const string FullCfgName = "io_t_full.cfg";  // always-ENOSPC device

        /// <summary>
        /// Create the fixtures in <paramref name="workDir"/> and return a
        /// handle that tears them down on Dispose. Always succeeds: if a
        /// platform cannot supply a given device, the matching .cfg simply is
        /// not written and the dependent test will SKIP.
        /// </summary>
        public static IDisposable Prepare(string workDir)
        {
            Directory.CreateDirectory(workDir);
            return OperatingSystem.IsWindows()
                ? PrepareWindows(workDir)
                : PrepareUnix(workDir);
        }

        // ----------------------------------------------------------------- Unix

        private static IDisposable PrepareUnix(string workDir)
        {
            var scope = new FixtureScope(workDir);

            // 175 / 296: a FIFO is non-seekable; lseek() fails on it, which is
            // exactly the condition zysrw/zysst report as "does not permit".
            string fifo = Path.Combine(workDir, "p_fifo");
            try
            {
                File.Delete(fifo);
            }
            catch { /* ignore */ }

            if (Mkfifo(fifo, 0b110_100_100 /* 0644 */) == 0)
            {
                // Hold the FIFO open O_RDWR for the lifetime of the scope so the
                // test's O_RDONLY open returns immediately and never hits EOF.
                int fd = Open(fifo, O_RDWR | O_NONBLOCK);
                if (fd >= 0)
                {
                    byte[] seed = System.Text.Encoding.ASCII.GetBytes("seed line\n");
                    Write(fd, seed, seed.Length);
                    scope.UnixFd = fd;
                    scope.WriteCfg(DevCfgName, "p_fifo");
                }
            }

            // 207 / 094: /dev/full accepts opens but every write returns ENOSPC.
            if (File.Exists("/dev/full") || CharDeviceExists("/dev/full"))
                scope.WriteCfg(FullCfgName, "/dev/full");

            return scope;
        }

        // -------------------------------------------------------------- Windows

        private static IDisposable PrepareWindows(string workDir)
        {
            var scope = new FixtureScope(workDir);

            // 175 / 296 on Windows: a named pipe is non-seekable. Whether the
            // osint port's open() accepts a \\.\pipe\ path determines whether
            // the seek actually raises 175 rather than failing benignly; the
            // strict = 0 assertion absorbs either outcome. The server end is
            // held open by the scope. If the port cannot open pipe paths,
            // delete this block (or leave it: the test will simply SKIP).
            try
            {
                string pipeName = "spitbol_test_nonseek_" + Environment.ProcessId;
                var server = new System.IO.Pipes.NamedPipeServerStream(
                    pipeName,
                    System.IO.Pipes.PipeDirection.InOut,
                    1,
                    System.IO.Pipes.PipeTransmissionMode.Byte,
                    System.IO.Pipes.PipeOptions.Asynchronous);
                scope.WinPipe = server;
                scope.WriteCfg(DevCfgName, @"\\.\pipe\" + pipeName);
            }
            catch
            {
                // No non-seekable device available -> rewind/set SKIP.
            }

            // 207 / 094 on Windows: there is no built-in always-ENOSPC device
            // (NUL discards writes and always succeeds). Leave io_t_full.cfg
            // unwritten so output.sbl / eject.sbl SKIP those codes. To enable
            // them, mount a tiny full volume and set SPITBOL_FULL_DEVICE to its
            // path; we honor that override if present.
            string? fullOverride = Environment.GetEnvironmentVariable("SPITBOL_FULL_DEVICE");
            if (!string.IsNullOrWhiteSpace(fullOverride))
                scope.WriteCfg(FullCfgName, fullOverride);

            return scope;
        }

        // ----------------------------------------------------------- teardown

        private sealed class FixtureScope : IDisposable
        {
            private readonly string _dir;
            public int UnixFd = -1;
            public IDisposable? WinPipe;
            private bool _disposed;

            public FixtureScope(string dir) => _dir = dir;

            public void WriteCfg(string name, string content)
                => File.WriteAllText(Path.Combine(_dir, name), content + "\n");

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (UnixFd >= 0)
                {
                    Close(UnixFd);
                    UnixFd = -1;
                }
                WinPipe?.Dispose();
                WinPipe = null;

                foreach (var n in new[] { DevCfgName, FullCfgName, "p_fifo" })
                {
                    try { File.Delete(Path.Combine(_dir, n)); } catch { /* ignore */ }
                }
            }
        }

        private static bool CharDeviceExists(string path)
        {
            try { return new FileInfo(path).Exists; } catch { return false; }
        }

        // --------------------------------------------------------- libc P/Invoke

        private const int O_RDWR     = 0x0002;
        private const int O_NONBLOCK = 0x0800; // Linux value

        [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
        private static extern int Mkfifo(string pathname, uint mode);

        [DllImport("libc", SetLastError = true, EntryPoint = "open")]
        private static extern int Open(string pathname, int flags);

        [DllImport("libc", SetLastError = true, EntryPoint = "write")]
        private static extern nint Write(int fd, byte[] buf, nint count);

        [DllImport("libc", SetLastError = true, EntryPoint = "close")]
        private static extern int Close(int fd);
    }
}
