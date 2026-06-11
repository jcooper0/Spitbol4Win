using System.Diagnostics;

namespace SpitbolTests;

public sealed record RunResult(
    int? ExitCode,        // null when the run was killed for timing out
    string StdOut,
    string StdErr,
    bool TimedOut,
    TimeSpan Duration);

/// <summary>
/// Runs spitbol on a single source file in that file's own directory (so
/// -INCLUDE 'foo.inc' and relative file I/O resolve), feeds an optional
/// stdin file, captures stdout/stderr concurrently, and kills the process
/// tree if it overruns the timeout.
/// </summary>
public static class SpitbolRunner
{
    public static async Task<RunResult> RunAsync(
        string exe, string sourceFile, string? stdinFile, TimeSpan timeout)
    {
        var workingDir = Path.GetDirectoryName(Path.GetFullPath(sourceFile))!;

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Path.GetFileName(sourceFile));

        var sw = Stopwatch.StartNew();
        using var process = new Process { StartInfo = psi };
        process.Start();

        // Feed stdin (or close it immediately so INPUT sees EOF instead of hanging).
        try
        {
            if (stdinFile is not null && File.Exists(stdinFile))
                await process.StandardInput.WriteAsync(await File.ReadAllTextAsync(stdinFile));
        }
        finally
        {
            process.StandardInput.Close();
        }

        // Read both streams concurrently to avoid pipe-buffer deadlock.
        var outTask = process.StandardOutput.ReadToEndAsync();
        var errTask = process.StandardError.ReadToEndAsync();

        var timedOut = false;
        using (var cts = new CancellationTokenSource(timeout))
        {
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            }
        }

        // Ensure the process is fully reaped so the read tasks complete.
        try { process.WaitForExit(); } catch { /* ignore */ }
        var stdout = await outTask;
        var stderr = await errTask;
        sw.Stop();

        int? exit = null;
        if (!timedOut)
        {
            try { exit = process.ExitCode; } catch { /* ignore */ }
        }

        return new RunResult(exit, stdout, stderr, timedOut, sw.Elapsed);
    }
}
