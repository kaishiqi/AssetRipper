using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.MissingReferences;

public sealed class MissingReferencesReport
{
	public string ExportVersion { get; set; } = string.Empty;
	public string PlaceholderGuid { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public int TotalOccurrences { get; set; }
	public List<MissingReferenceInfo> References { get; set; } = new();

	/// <summary>
	/// Pointers that were already null in the source data. They are listed separately because
	/// <c>{fileID: 0}</c> is not an export failure and must not be repaired by post-processing.
	/// </summary>
	public int NullInSourceCount { get; set; }
	public int NullInSourceOccurrences { get; set; }
	public bool NullInSourceTruncated { get; set; }
	public List<MissingReferenceInfo> NullInSourceReferences { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MissingReferencesReport))]
internal sealed partial class MissingReferencesSerializerContext : JsonSerializerContext
{
}
