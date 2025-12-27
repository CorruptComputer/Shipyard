using System.Text.Json.Serialization;

namespace Shipyard.Models;

/// <summary>
///   The options for .NET publishing.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DotnetPublishOptions
{
    /// <summary>
    ///   Trim unused assemblies from the published application.
    /// </summary>
    Trim,

    /// <summary>
    ///   Publish as a self-contained application.
    /// </summary>
    SelfContained
}
