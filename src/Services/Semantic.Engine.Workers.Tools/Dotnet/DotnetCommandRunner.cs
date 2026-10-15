using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Semantic.Engine.Workers.Tools.Options;

namespace Semantic.Engine.Workers.Tools.Dotnet;

/// <summary>
/// Runs the <c>dotnet</c> CLI and captures its output.
/// </summary>
public sealed class DotnetCommandRunner(IOptions<ToolsWorkerOptions> options)
{
    /// <summary>
    /// Runs <c>dotnet</c> with the given arguments.
    /// </summary>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="cancellationToken">A token that cancels the command and kills the process tree.</param>
    /// <returns>The exit code and the combined output. A timeout is reported with <see cref="DotnetCommandResult.TimedOut"/>.</returns>
    public async Task<DotnetCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using Process process = new() { StartInfo = startInfo };
        StringBuilder output = new();
        object outputLock = new();
        void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (outputLock)
            {
                output.AppendLine(line);
            }
        }

        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.DotnetTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            // Flush the asynchronous output readers
            process.WaitForExit();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return new DotnetCommandResult(-1, output.ToString(), TimedOut: true);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new DotnetCommandResult(process.ExitCode, output.ToString(), TimedOut: false);
    }
}

/// <summary>
/// The outcome of a <c>dotnet</c> invocation.
/// </summary>
/// <param name="ExitCode">The process exit code; -1 on timeout.</param>
/// <param name="Output">The combined standard output and standard error.</param>
/// <param name="TimedOut">Whether the process was killed because it exceeded the configured timeout.</param>
public sealed record DotnetCommandResult(int ExitCode, string Output, bool TimedOut);
