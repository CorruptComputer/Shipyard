using System.Diagnostics;

namespace Shipyard.Wrappers;

/// <summary>
///   Handles invoking dotnet commands like publish.
/// </summary>
public class DotnetWrapper(TextWriter consoleWriter, TextWriter errorWriter)
{
    /// <summary>
    ///   Publishes a .NET project.
    /// </summary>
    /// <param name="projectFile">Path to the project file</param>
    /// <param name="outputDirectory">Directory to publish to</param>
    /// <param name="configuration"></param>
    /// <param name="framework">Target framework (e.g., net9.0)</param>
    /// <param name="runtime">Runtime identifier (e.g., linux-x64)</param>
    /// <param name="selfContained"></param>
    /// <param name="trim"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the published directory</returns>
    /// <exception cref="InvalidOperationException">Thrown when the publish process fails</exception>
    public async Task<string?> PublishAsync(
        string projectFile,
        string outputDirectory,
        string configuration,
        string framework,
        string runtime,
        bool selfContained = false,
        bool trim = false,
        CancellationToken cancellationToken = default)
    {
        // Build the publish arguments
        List<string> args =
        [
            "publish",
            projectFile,
            "--output", outputDirectory,
            "--configuration", configuration,
            "--framework", framework,
            "--runtime", runtime,
        ];

        if (selfContained)
        {
            args.Add("--self-contained");
        }
        else
        {
            args.Add("--no-self-contained");
        }

        if (trim)
        {
            args.Add("/p:PublishTrimmed=true");
        }

        ProcessStartInfo psi = new("dotnet", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet publish process.");

        await process.WaitForExitAsync(cancellationToken);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            await errorWriter.WriteLineAsync($"dotnet publish failed with exit code {process.ExitCode}");
            if (!string.IsNullOrWhiteSpace(error))
            {
                await errorWriter.WriteLineAsync(error);
            }

            return null;
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            await consoleWriter.WriteLineAsync(output);
        }

        return outputDirectory;
    }
}