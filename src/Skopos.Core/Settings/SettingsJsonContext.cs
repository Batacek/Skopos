using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skopos.Core.Settings;

/// <summary>
/// Reflection-based serialization does not work under NativeAOT, hence the source generator.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SkoposSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
