using Shipyard.Models;
using Shipyard.Models.ProjectConfiguration;
using Shipyard.Models.ProjectConfiguration.FormatConfiguration;
using Shipyard.Templates;
using Shipyard.Wrappers;

namespace Shipyard.Packagers;

/// <summary>
///   The RPM packager.
/// </summary>
/// <param name="sourceDir"></param>
/// <param name="outputDir"></param>
/// <param name="projectConfig"></param>
/// <param name="templateResults"></param>
/// <param name="consoleWriter"></param>
/// <param name="errorWriter"></param>
public class RpmPackager(DirectoryInfo sourceDir, DirectoryInfo outputDir, ProjectConfig projectConfig, IEnumerable<TemplateResult> templateResults, TextWriter consoleWriter, TextWriter errorWriter)
    : PackagerBase<RpmConfig>(sourceDir, outputDir, projectConfig, templateResults, consoleWriter, errorWriter)
{
    /// <inheritdoc/>
    protected override async Task<PackageResult> CreatePackageForFormatAsync(RpmConfig formatConfig, TemplateResult? buildTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(buildTemplate);

        try
        {
            string packagePath = await BuildRpmAsync(formatConfig, buildTemplate);
            return new("rpm", packagePath, true);
        }
        catch (Exception ex)
        {
            return new("rpm", string.Empty, false, ex);
        }
    }

    /// <summary>
    ///   Builds the RPM package from the published directory.
    /// </summary>
    private async Task<string> BuildRpmAsync(RpmConfig config, TemplateResult buildTemplate)
    {
        if (!ProjectConfig.HasNonNullRequiredValues || !config.HasNonNullRequiredValues)
        {
            throw new InvalidOperationException("Project configuration or RPM configuration is missing required values.");
        }

        ConsoleWriter.WriteLine($"Building RPM package for runtime {buildTemplate.Runtime}...");

        if (!Directory.Exists(Path.Combine(OutputDir.ToString(), $"publish-{buildTemplate.Runtime}")))
        {
            throw new DirectoryNotFoundException($"Published directory not found for runtime {buildTemplate.Runtime}");
        }

        RpmBuildDirectories rpmDirs = CreateRpmBuildDirectoryStructure(OutputDir, buildTemplate.Runtime.ToJsonValue(), config.PackageName, ProjectConfig.Version);

        // Copy the publish files to BUILDROOT
        bool copied = CopyPublishedFilesToBuildRoot(
            new DirectoryInfo(Path.Combine(OutputDir.ToString(), $"publish-{buildTemplate.Runtime}")),
            rpmDirs.BuildRootDir,
            config.PackageName);

        if (!copied)
        {
            throw new IOException("Failed to copy published files to RPM build root.");
        }

        // Create the symlink
        bool symlinkCreated = CreateSymbolicLink(
            config.PackageName,
            rpmDirs.BuildRootDir);
        if (!symlinkCreated)
        {
            throw new IOException("Failed to create symbolic link in RPM build root.");
        }

        // If enabled, copy the systemd service file from the
        if (config.InstallSystemdService.HasValue
            && config.InstallSystemdService.Value
            && !string.IsNullOrWhiteSpace(config.SystemdServiceName))
        {
            bool systemdCopied = CopySystemdServiceFileToBuildRoot(
                SourceDir,
                rpmDirs.BuildRootDir,
                config.SystemdServiceName!);

            if (!systemdCopied)
            {
                throw new IOException("Failed to copy systemd service file to RPM build root.");
            }
        }

        RpmBuildWrapper rpmBuilder = new(ConsoleWriter, ErrorWriter);
        string? builtRpmPath = await rpmBuilder.BuildRpmAsync(
            buildTemplate.OutputFile.FullName,
            rpmDirs.RpmbuildRoot.FullName);

        if (string.IsNullOrWhiteSpace(builtRpmPath))
        {
            throw new InvalidOperationException("RPM build failed.");
        }

        return builtRpmPath;
    }

    private sealed record RpmBuildDirectories(
        DirectoryInfo RpmbuildRoot,
        DirectoryInfo BuildDir,
        DirectoryInfo BuildRootDir);

    private static RpmBuildDirectories CreateRpmBuildDirectoryStructure(DirectoryInfo outputDir, string runtime, string packageName, string version)
    {
        // Set up rpmbuild directory structure
        DirectoryInfo rpmbuildRoot = outputDir.CreateSubdirectory(Path.Combine("rpmbuild", runtime));
        DirectoryInfo buildDir = rpmbuildRoot.CreateSubdirectory("BUILD");
        DirectoryInfo buildRootDir = buildDir.CreateSubdirectory(Path.Combine($"{packageName}-{version}-build", "BUILDROOT"));

        return new RpmBuildDirectories(rpmbuildRoot, buildDir, buildRootDir);
    }

    private bool CopyPublishedFilesToBuildRoot(DirectoryInfo publishedDir, DirectoryInfo buildRootDir, string packageName)
    {
        try
        {
            DirectoryInfo targetDir = buildRootDir.CreateSubdirectory(Path.Combine("usr", "share", packageName));

            foreach (string dirPath in Directory.GetDirectories(publishedDir.FullName, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(publishedDir.FullName, targetDir.FullName));
            }

            foreach (string newPath in Directory.GetFiles(publishedDir.FullName, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(publishedDir.FullName, targetDir.FullName), true);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying published files to build root: {ex.Message}");
            return false;
        }
    }

    private bool CreateSymbolicLink(string packageName, DirectoryInfo buildRootDir)
    {
        try
        {
            DirectoryInfo symlinkDir = buildRootDir.CreateSubdirectory(Path.Combine("usr", "local", "bin"));
            string symlinkPath = Path.Combine(symlinkDir.FullName, packageName);
            File.CreateSymbolicLink(symlinkPath, Path.Combine("../../share", packageName, packageName));
            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error creating symbolic link: {ex.Message}");
            return false;
        }
    }

    private bool CopySystemdServiceFileToBuildRoot(DirectoryInfo sourcePath, DirectoryInfo buildRootDir, string systemdServiceName)
    {
        try
        {
            DirectoryInfo serviceDir = buildRootDir.CreateSubdirectory(Path.Combine("etc", "systemd", "system"));
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
