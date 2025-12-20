using System.Diagnostics;

namespace Shipyard.Wrappers;

/// <summary>
///   Handles building RPM packages.
/// </summary>
public class RpmBuildWrapper(TextWriter consoleWriter, TextWriter errorWriter)
{
    /// <summary>
    ///   Builds an RPM package using rpmbuild.
    /// </summary>
    /// <param name="specFile">Path to the .spec file</param>
    /// <param name="buildTopDir">Top-level directory for rpmbuild (usually BUILDROOT or similar)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the built RPM file if successful; null otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown when the rpmbuild process fails to start</exception>
    public async Task<string?> BuildRpmAsync(
        string specFile,
        string buildTopDir,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(specFile))
        {
            await errorWriter.WriteLineAsync($"Spec file not found: {specFile}");
            return null;
        }

        List<string> args =
        [
            "-bb",
            "--define", $"_topdir {buildTopDir}",
            "--noclean",
            "--noprep",
            specFile
        ];

        ProcessStartInfo psi = new()
        {
            FileName = "rpmbuild",
            Arguments = string.Join(" ", args.Select(arg => $"\"{arg}\"")),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start rpmbuild process.");

        await process.WaitForExitAsync(cancellationToken);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            await errorWriter.WriteLineAsync($"rpmbuild failed with exit code {process.ExitCode}");
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

        string? rpmPath = ExtractRpmPath(output);
        return rpmPath;
    }

    /// <summary>
    ///   Extracts the RPM file path from rpmbuild output.
    /// </summary>
    private static string? ExtractRpmPath(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // rpmbuild outputs lines like: "Wrote: /path/to/package.rpm"
        string[] lines = output.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (line.Contains("Wrote:"))
            {
                string[] parts = line.Split("Wrote:", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    return parts[0].Trim();
                }
            }
        }

        return null;
    }
}
