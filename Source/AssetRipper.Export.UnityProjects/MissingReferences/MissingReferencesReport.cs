using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.MissingReferences;

public sealed class MissingReferencesReport
{
	public string ExportVersion { get; set; } = string.Empty;
	public string PlaceholderGuid { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public List<MissingReferenceInfo> References { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MissingReferencesReport))]
internal sealed partial class MissingReferencesSerializerContext : JsonSerializerContext
{
}
