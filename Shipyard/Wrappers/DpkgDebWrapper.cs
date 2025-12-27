using System.Diagnostics;

namespace Shipyard.Wrappers;

/// <summary>
///   Wrapper for invoking dpkg-deb.
/// </summary>
/// <param name="consoleWriter">Writer for standard output</param>
/// <param name="errorWriter">Writer for error output</param>
public sealed class DpkgDebWrapper(TextWriter consoleWriter, TextWriter errorWriter)
{
    /// <summary>
    ///   Builds a DEB package using dpkg-deb.
    /// </summary>
    /// <param name="packageRoot"></param>
    /// <param name="outputFile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string?> BuildAsync(
        string outputFile,
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        List<string> args =
        [
            "--build",
            "--root-owner-group",
            packageRoot,
            outputFile
        ];

        ProcessStartInfo psi = new("dpkg-deb", args)
        {
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
            await errorWriter.WriteLineAsync($"dpkg-deb failed with exit code {process.ExitCode}");
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

        string? rpmPath = ExtractDebPath(output);
        return rpmPath;
    }

    /// <summary>
    ///   Extracts the DEB file path from dpkg-deb output.
    /// </summary>
    private static string? ExtractDebPath(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // building package 'shipyard.demo' in '/mnt/cueball/Code/Shipyard/publish/Shipyard.Demo-0.0.1-amd64.deb'.

        string marker = "in '";
        int markerIndex = output.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            int startIndex = markerIndex + marker.Length;
            int endIndex = output.IndexOf("'.", startIndex, StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return output[startIndex..endIndex];
            }
        }

        return null;
    }
}
