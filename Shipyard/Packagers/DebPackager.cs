using System;
using Shipyard.Models;
using Shipyard.Models.Configuration;
using Shipyard.Models.Configuration.OutputFormats;
using Shipyard.Templates;
using Shipyard.Wrappers;

namespace Shipyard.Packagers;

/// <summary>
///   Packager for DEB packages.
/// </summary>
/// <param name="sourceDir"></param>
/// <param name="outputDir"></param>
/// <param name="workingDir"></param>
/// <param name="projectConfig"></param>
/// <param name="templateResults"></param>
/// <param name="consoleWriter"></param>
/// <param name="errorWriter"></param>
public class DebPackager(DirectoryInfo sourceDir, DirectoryInfo outputDir, DirectoryInfo workingDir, ShipyardConfig projectConfig,
                         IEnumerable<TemplateResult> templateResults, TextWriter consoleWriter, TextWriter errorWriter)
    : PackagerBase<DebConfig>(sourceDir, outputDir, workingDir, projectConfig, templateResults, consoleWriter, errorWriter)
{
    /// <inheritdoc />
    protected override async Task<PackageResult> CreatePackageForFormatAsync(DebConfig formatConfig, TemplateResult? buildTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(buildTemplate);

        try
        {
            string packagePath = await BuildDebAsync(formatConfig, buildTemplate);
            return new("deb", packagePath, true);
        }
        catch (Exception ex)
        {
            return new("deb", string.Empty, false, ex);
        }
    }

    /// <summary>
    ///   Builds the DEB package from the published directory.
    /// </summary>
    private async Task<string> BuildDebAsync(DebConfig config, TemplateResult buildTemplate)
    {
        if (!ProjectConfig.HasNonNullRequiredValues || !config.HasNonNullRequiredValues)
        {
            throw new InvalidOperationException("Project configuration or DEB configuration is missing required values.");
        }

        ConsoleWriter.WriteLine($"Building DEB package for runtime {buildTemplate.Runtime}...");

        if (!Directory.Exists(Path.Combine(WorkingDir.ToString(), $"publish-{buildTemplate.Runtime}")))
        {
            throw new DirectoryNotFoundException($"Published directory not found for runtime {buildTemplate.Runtime}");
        }

        DirectoryInfo buildRoot = CreateDebBuildRoot(WorkingDir, buildTemplate.Runtime.ToJsonValue());

        // Copy the publish files to BUILDROOT
        bool copied = CopyPublishedFilesToBuildRoot(
            new DirectoryInfo(Path.Combine(WorkingDir.ToString(), $"publish-{buildTemplate.Runtime}")),
            buildRoot,
            config.PackageName);

        if (!copied)
        {
            throw new IOException("Failed to copy published files to DEB build root.");
        }

        bool controlCopied = CopyControlFileToBuildRoot(
            SourceDir,
            buildRoot,
            buildTemplate.OutputFile.FullName);

        if (!controlCopied)
        {
            throw new IOException("Failed to copy control file to DEB build root.");
        }

        // Create symlink for executable
        bool symlinked = CreateSymlinkForExecutable(
            buildRoot,
            config.PackageName,
            ProjectConfig.Executable);

        if (!symlinked)
        {
            throw new IOException("Failed to create symlink for executable in DEB build root.");
        }

        // If enabled, copy the systemd service file from the
        if (config.InstallSystemdService.HasValue
            && config.InstallSystemdService.Value
            && !string.IsNullOrWhiteSpace(config.SystemdServiceName))
        {
            bool systemdCopied = CopySystemdServiceFileToBuildRoot(
                SourceDir,
                buildRoot,
                config.SystemdServiceName!);

            if (!systemdCopied)
            {
                throw new IOException("Failed to copy systemd service file to DEB build root.");
            }
        }

        DpkgDebWrapper debBuilder = new(ConsoleWriter, ErrorWriter);
        string? builtDebPath = await debBuilder.BuildAsync(
            Path.Combine(OutputDir.FullName, $"{config.PackageName}-{ProjectConfig.Version}-{buildTemplate.Runtime.ToDebArchString()}.deb"),
            buildRoot.FullName);

        if (string.IsNullOrWhiteSpace(builtDebPath))
        {
            throw new InvalidOperationException("DEB build failed.");
        }

        string finalDebPath = Path.Combine(OutputDir.FullName, Path.GetFileName(builtDebPath));

        File.Move(builtDebPath, finalDebPath, overwrite: true);

        return finalDebPath;
    }

    private static DirectoryInfo CreateDebBuildRoot(DirectoryInfo workingDir, string runtime)
    {
        // Set up dpkg directory structure
        DirectoryInfo debbuildRoot = workingDir.CreateSubdirectory(Path.Combine("dpkg-deb", runtime));

        return debbuildRoot;
    }

    private static bool CopyControlFileToBuildRoot(DirectoryInfo sourcePath, DirectoryInfo buildRootDir, string controlFileName)
    {
        try
        {
            DirectoryInfo controlDir = buildRootDir.CreateSubdirectory("DEBIAN");
            File.Copy(Path.Combine(sourcePath.FullName, controlFileName), Path.Combine(controlDir.FullName, "control"));

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool CopyPublishedFilesToBuildRoot(DirectoryInfo publishedDir, DirectoryInfo buildRootDir, string packageName)
    {
        try
        {
            DirectoryInfo packageDir = buildRootDir.CreateSubdirectory("usr").CreateSubdirectory("share").CreateSubdirectory(packageName);

            foreach (string dirPath in Directory.GetDirectories(publishedDir.FullName, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(publishedDir.FullName, packageDir.FullName));
            }

            foreach (string newPath in Directory.GetFiles(publishedDir.FullName, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(publishedDir.FullName, packageDir.FullName));
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying published files to build root: {ex.Message}");
            return false;
        }
    }

    private bool CreateSymlinkForExecutable(DirectoryInfo buildRootDir, string packageName, string executableName)
    {
        try
        {
            DirectoryInfo symlinkDir = buildRootDir.CreateSubdirectory("usr").CreateSubdirectory("local").CreateSubdirectory("bin");

            // ../../share/{{ package_name }}/{{ executable_name }} %{buildroot}/usr/local/bin/{{ executable_name }}
            File.CreateSymbolicLink($"{symlinkDir.FullName}/{executableName}", $"../../share/{packageName}/{executableName}");

            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error creating symlink for executable: {ex.Message}");
            return false;
        }
    }

    private bool CopySystemdServiceFileToBuildRoot(DirectoryInfo sourcePath, DirectoryInfo buildRootDir, string systemdServiceName)
    {
        try
        {
            DirectoryInfo serviceDir = buildRootDir.CreateSubdirectory("etc").CreateSubdirectory("systemd").CreateSubdirectory("system");
            File.Copy(Path.Combine(sourcePath.FullName, systemdServiceName), Path.Combine(serviceDir.FullName, systemdServiceName));

            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying systemd service file: {ex.Message}");
            return false;
        }
    }
}
