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
    /// <param name="framework">Target framework (e.g., net9.0)</param>
    /// <param name="runtime">Runtime identifier (e.g., linux-x64)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the published directory</returns>
    /// <exception cref="InvalidOperationException">Thrown when the publish process fails</exception>
    public async Task<string?> PublishAsync(
        string projectFile,
        string outputDirectory,
        string framework,
        string runtime,
        CancellationToken cancellationToken = default)
    {
        // Build the publish arguments
        List<string> args =
        [
            "publish",
            projectFile,
            "--output", outputDirectory,
            "--configuration", "Release", // TODO: Figure out, does this need to be configurable?
            "--framework", framework,
            "--runtime", runtime,
            "--no-self-contained", // TODO: Figure out, does this need to be configurable?
            "--verbosity", "minimal" // TODO: Figure out, does this need to be configurable?
        ];

        ProcessStartInfo psi = new()
        {
            FileName = "dotnet", // TODO: Figure out, does this need to be configurable to provide a full path?
            Arguments = string.Join(" ", args.Select(arg => $"\"{arg}\"")),
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