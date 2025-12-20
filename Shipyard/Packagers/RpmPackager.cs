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


        // Copy the source files from the .csproj directory to SOURCES, the csproj directory should be resolved relative to the config file
        bool sourceCopied = CopySourceFilesToSourceDir(
            SourceDir,
            rpmDirs.SourcesDir);

        if (!sourceCopied)
        {
            throw new IOException("Failed to copy source files to RPM SOURCES directory.");
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

        // Copy the specfile from buildTemplate.OutputFile to its location in SPECS
        bool specCopied = CopySpecFileToSpecsDir(
            buildTemplate.OutputFile,
            rpmDirs.SpecsDir);

        if (!specCopied)
        {
            throw new IOException("Failed to copy spec file to RPM SPECS directory.");
        }

        RpmBuildWrapper rpmBuilder = new(ConsoleWriter, ErrorWriter);
        string? builtRpmPath = await rpmBuilder.BuildRpmAsync(
            buildTemplate.OutputFile.FullName,
            rpmDirs.RpmbuildRoot.FullName);

        if (builtRpmPath is null)
        {
            throw new InvalidOperationException("RPM build failed.");
        }

        return builtRpmPath;
    }

    private sealed record RpmBuildDirectories(
        DirectoryInfo RpmbuildRoot,
        DirectoryInfo BuildDir,
        DirectoryInfo BuildRootDir,
        DirectoryInfo SpecsDir,
        DirectoryInfo RpmsDir,
        DirectoryInfo SourcesDir);

    private static RpmBuildDirectories CreateRpmBuildDirectoryStructure(DirectoryInfo outputDir, string runtime, string packageName, string version)
    {
        // Set up rpmbuild directory structure
        DirectoryInfo rpmbuildRoot = outputDir.CreateSubdirectory(Path.Combine("rpmbuild", runtime));
        DirectoryInfo buildDir = rpmbuildRoot.CreateSubdirectory("BUILD");
        DirectoryInfo buildRootDir = buildDir.CreateSubdirectory(Path.Combine($"{packageName}-{version}-build", "BUILDROOT"));

        // Copy specfile here
        DirectoryInfo specsDir = rpmbuildRoot.CreateSubdirectory("SPECS");

        // The RPM will be created in here after rpmbuild is run
        DirectoryInfo rpmsDir = rpmbuildRoot.CreateSubdirectory("RPMS");

        // Put the source code here (not strictly necessary, but a nice to have)
        DirectoryInfo sourcesDir = rpmbuildRoot.CreateSubdirectory("SOURCES");

        return new RpmBuildDirectories(
            rpmbuildRoot,
            buildDir,
            buildRootDir,
            specsDir,
            rpmsDir,
            sourcesDir);
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
            string symlinkTarget = Path.Combine("../../share", packageName, packageName);
            DirectoryInfo symlinkDir = buildRootDir.CreateSubdirectory(Path.Combine("usr", "local", "bin"));
            string symlinkPath = Path.Combine(symlinkDir.FullName, packageName);

            Directory.CreateDirectory(Path.GetDirectoryName(symlinkPath)!);
            File.CreateSymbolicLink(symlinkPath, symlinkTarget);
            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error creating symbolic link: {ex.Message}");
            return false;
        }
    }

    private bool CopySourceFilesToSourceDir(DirectoryInfo sourceFilesDir, DirectoryInfo sourceDir)
    {
        string[] excludeDirs =
        [
            Path.Combine(sourceDir.FullName, "bin"),
            Path.Combine(sourceDir.FullName, "obj"),
            Path.Combine(sourceDir.FullName, "publish"),
        ];

        try
        {
            foreach (string dirPath in Directory.GetDirectories(sourceFilesDir.FullName, "*", SearchOption.AllDirectories))
            {
                if (excludeDirs.Any(ed => dirPath.StartsWith(ed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Directory.CreateDirectory(dirPath.Replace(sourceFilesDir.FullName, sourceDir.FullName));
            }

            foreach (string newPath in Directory.GetFiles(sourceFilesDir.FullName, "*.*", SearchOption.AllDirectories))
            {
                if (excludeDirs.Any(ed => newPath.StartsWith(ed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                File.Copy(newPath, newPath.Replace(sourceFilesDir.FullName, sourceDir.FullName), true);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying source files to SOURCES directory: {ex.Message}");
            return false;
        }
    }

    private bool CopySystemdServiceFileToBuildRoot(DirectoryInfo sourcePath, DirectoryInfo buildRootDir, string systemdServiceName)
    {
        try
        {
            string destPath = Path.Combine(buildRootDir.FullName, "etc", "systemd", "system", systemdServiceName);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            File.Copy(Path.Combine(sourcePath.FullName, systemdServiceName), destPath, true);
            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying systemd service file: {ex.Message}");
            return false;
        }
    }

    private bool CopySpecFileToSpecsDir(FileInfo specFile, DirectoryInfo specsDir)
    {
        try
        {
            string destPath = Path.Combine(specsDir.FullName, specFile.Name);
            File.Copy(specFile.FullName, destPath, true);
            return true;
        }
        catch (Exception ex)
        {
            ErrorWriter.WriteLine($"Error copying spec file to SPECS directory: {ex.Message}");
            return false;
        }
    }
}
