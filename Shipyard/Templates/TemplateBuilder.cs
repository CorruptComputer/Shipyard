using Scriban;
using Scriban.Parsing;
using Shipyard.Models;
using Shipyard.Models.ProjectConfiguration;
using Shipyard.Models.ProjectConfiguration.FormatConfiguration;

namespace Shipyard.Templates;

/// <summary>
///   The template builder.
/// </summary>
/// <param name="projectConfig"></param>
/// <param name="outputDir"></param>
public class TemplateBuilder(ProjectConfig projectConfig, DirectoryInfo outputDir)
{
    /// <summary>
    ///   Builds templates for all specified formats.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task<List<TemplateResult>> BuildTemplatesAsync()
    {
        if (!projectConfig.HasNonNullRequiredValues)
        {
            throw new InvalidOperationException("Project configuration is missing required values.");
        }

        List<TemplateResult> results = [];

        foreach (DotnetRuntimes runtime in projectConfig.Runtimes)
        {
            foreach (FormatConfigurationBase config in projectConfig.FormatConfigs)
            {
                switch (config)
                {
                    case RpmConfig rpmConfig:
                        results.Add(await BuildRpmTemplateAsync(runtime, rpmConfig));
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported format configuration type: {config.GetType().Name}");
                }
            }
        }

        return results;
    }

    private async Task<TemplateResult> BuildRpmTemplateAsync(DotnetRuntimes runtime, RpmConfig config)
    {
        var templateModel = new
        {
            package_name = config.PackageName,
            version = projectConfig.Version,
            release = config.Release,
            build_arch = runtime.ToRpmArchString(),
            summary = "TODO: Add summary",
            license = projectConfig.License,
            vendor = projectConfig.Author,
            url = projectConfig.RepositoryUrl,
            author = projectConfig.Author,
            depends_on = config.DependsOn,
            provides = config.Provides,
            //post_install_script = "TODO: Figure out what the hell goes here",
            description = "TODO: Add description",
            systemd_service_name = config.InstallSystemdService == true ? config.SystemdServiceName : null,
            pre_install_script = config.PreInstallScript,
            post_install_script = config.PostInstallScript,
            pre_uninstall_script = config.PreUninstallScript,
            post_uninstall_script = config.PostUninstallScript
        };

        string templatePath = Path.Combine(
            Path.GetDirectoryName(typeof(TemplateBuilder).Assembly.Location) ?? ".",
            "Templates/rpm_specfile.scriban");

        string templateContent = File.ReadAllText(templatePath);
        Template template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            foreach (LogMessage message in template.Messages)
            {
                Console.Error.WriteLine($"Template parsing error: {message.Message}");
            }

            throw new InvalidOperationException($"Error parsing template: {string.Join(", ", template.Messages.Select(m => m.Message))}");
        }
        string result = await template.RenderAsync(templateModel);
        string outputFilePath = Path.Combine(outputDir.FullName, $"{config.PackageName}-{runtime.ToRpmArchString()}.spec");

        await File.WriteAllTextAsync(outputFilePath, result);
        return new TemplateResult(PackageFormat.rpm, runtime, new FileInfo(outputFilePath));
    }
}
