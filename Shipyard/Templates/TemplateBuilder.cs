using Scriban;
using Scriban.Parsing;
using Shipyard.Models;
using Shipyard.Models.Configuration;
using Shipyard.Models.Configuration.OutputFormats;

namespace Shipyard.Templates;

/// <summary>
///   The template builder.
/// </summary>
/// <param name="config"></param>
/// <param name="outputDir"></param>
public class TemplateBuilder(ShipyardConfig config, DirectoryInfo outputDir)
{
    /// <summary>
    ///   Builds templates for all specified formats.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task<List<TemplateResult>> BuildTemplatesAsync()
    {
        if (!config.HasNonNullRequiredValues || !config.Dotnet.HasNonNullRequiredValues)
        {
            throw new InvalidOperationException("Project configuration is missing required values.");
        }

        List<TemplateResult> results = [];

        foreach (DotnetRuntimes runtime in config.Dotnet.Runtimes)
        {
            foreach (FormatConfigurationBase config in config.FormatConfigs)
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

    private async Task<TemplateResult> BuildRpmTemplateAsync(DotnetRuntimes runtime, RpmConfig rpmConfig)
    {
        if (!config.HasNonNullRequiredValues || !config.Dotnet.HasNonNullRequiredValues)
        {
            throw new InvalidOperationException("Project configuration is missing required values.");
        }

        var templateModel = new
        {
            package_name = rpmConfig.PackageName,
            version = config.Version,
            release = rpmConfig.Release,
            build_arch = runtime.ToRpmArchString(),
            summary = "TODO: Add summary",
            license = config.License,
            vendor = config.Author,
            url = config.RepositoryUrl,
            author = config.Author,
            depends_on = rpmConfig.DependsOn,
            provides = rpmConfig.Provides,
            //post_install_script = "TODO: Figure out what the hell goes here",
            description = "TODO: Add description",
            systemd_service_name = rpmConfig.InstallSystemdService == true ? rpmConfig.SystemdServiceName : null,
            pre_install_script = rpmConfig.PreInstallScript,
            post_install_script = rpmConfig.PostInstallScript,
            pre_uninstall_script = rpmConfig.PreUninstallScript,
            post_uninstall_script = rpmConfig.PostUninstallScript
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
        string outputFilePath = Path.Combine(outputDir.FullName, $"{rpmConfig.PackageName}-{runtime.ToRpmArchString()}.spec");

        await File.WriteAllTextAsync(outputFilePath, result);
        return new TemplateResult(PackageFormat.rpm, runtime, new FileInfo(outputFilePath));
    }
}
