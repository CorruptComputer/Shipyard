using Shipyard.Models;

namespace Shipyard.Templates;

/// <summary>
///   The result of building a template.
/// </summary>
/// <param name="Format">The package format.</param>
/// <param name="Runtime">The .NET runtime.</param>
/// <param name="OutputFile">The output file.</param>
public record TemplateResult(PackageFormat Format, DotnetRuntimes Runtime, FileInfo OutputFile);

