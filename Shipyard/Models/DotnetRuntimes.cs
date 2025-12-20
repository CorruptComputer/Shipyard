using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shipyard.Models;

/// <summary>
///   The supported .NET runtimes for packaging.<br />
///   See: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog#linux-rids<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonConverter(typeof(DotnetRuntimesConverter))]
public enum DotnetRuntimes
{
// These underscores are necessary because dashes are not allowed in enum member names
// I'd like these names to match the RID names as closely as possible
#pragma warning disable CA1707 // Remove the underscores from member names
    /// <summary>
    ///   Generic Linux runtime without architecture specification
    /// </summary>
    [JsonPropertyName("linux")]
    noarch,

    /// <summary>
    ///   Most desktop distributions like CentOS Stream, Debian, Fedora, Ubuntu, and derivatives
    /// </summary>
    [JsonPropertyName("linux-x64")]
    x64,

    /// <summary>
    ///   Lightweight distributions using musl like Alpine Linux
    /// </summary>
    [JsonPropertyName("linux-musl-x64")]
    musl_x64,

    /// <summary>
    ///   Used to build Docker images for 64-bit Arm v8 and minimalistic base images
    /// </summary>
    [JsonPropertyName("linux-musl-arm64")]
    musl_arm64,

    /// <summary>
    ///   Linux distributions running on Arm like Raspbian on Raspberry Pi Model 2+
    /// </summary>
    [JsonPropertyName("linux-arm")]
    arm,

    /// <summary>
    ///   Linux distributions running on 64-bit Arm like Ubuntu Server 64-bit on Raspberry Pi Model 3+
    /// </summary>
    [JsonPropertyName("linux-arm64")]
    arm64,

    /// <summary>
    ///   Distributions using Android's bionic libc, for example, Termux
    /// </summary>
    [JsonPropertyName("linux-bionic-arm64")]
    bionic_arm64,

    /// <summary>
    ///   Linux distributions running on LoongArch64
    /// </summary>
    [JsonPropertyName("linux-loongarch64")]
    loongarch64,
#pragma warning restore CA1707 // Remove the underscores from member names
}

internal static class DotnetRuntimesExtensions
{
    public static string ToJsonValue(this DotnetRuntimes runtime)
    {
        JsonPropertyNameAttribute? attribute = runtime
            .GetType()
            .GetMember(runtime.ToString())
            .First()
            .GetCustomAttribute<JsonPropertyNameAttribute>();

        if (attribute is null)
        {
            throw new InvalidDataException($"DotnetRuntimes value {runtime} is missing JsonPropertyNameAttribute.");
        }

        return attribute.Name;
    }

    public static string ToRpmArchString(this DotnetRuntimes runtime)
    {
        return runtime switch
        {
            DotnetRuntimes.noarch => "noarch",
            DotnetRuntimes.x64 => "x86_64",
            DotnetRuntimes.musl_x64 => "x86_64",
            DotnetRuntimes.musl_arm64 => "arm64",
            // I don't think Fedora supports 32 bit anything anymore
            //DotnetRuntimes.arm => "arm32",
            DotnetRuntimes.arm64 => "arm64",
            DotnetRuntimes.bionic_arm64 => "arm64",
            // No clue what this would even be
            //DotnetRuntimes.loongarch64 => "loongarch64",
            _ => throw new NotSupportedException($"Unsupported DotnetRuntimes value: {runtime}")
        };
    }
}

internal sealed class DotnetRuntimesConverter : JsonConverter<DotnetRuntimes>
{
    private static readonly Dictionary<string, DotnetRuntimes> NameToValue = [];
    private static readonly Dictionary<DotnetRuntimes, string> ValueToName = [];

    static DotnetRuntimesConverter()
    {
        foreach (DotnetRuntimes value in Enum.GetValues<DotnetRuntimes>())
        {
            JsonPropertyNameAttribute? attr = value.GetType()
                .GetMember(value.ToString())
                .First()
                .GetCustomAttribute<JsonPropertyNameAttribute>();

            string name = attr?.Name ?? value.ToString();
            NameToValue[name] = value;
            ValueToName[value] = name;
        }
    }

    public override DotnetRuntimes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? stringValue = reader.GetString();
        if (stringValue is not null && NameToValue.TryGetValue(stringValue, out DotnetRuntimes value))
        {
            return value;
        }
        throw new JsonException($"Unable to convert \"{stringValue}\" to enum type");
    }

    public override void Write(Utf8JsonWriter writer, DotnetRuntimes value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ValueToName[value]);
    }
}
