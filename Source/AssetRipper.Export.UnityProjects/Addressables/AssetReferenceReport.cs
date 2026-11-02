using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Addressables;

public sealed class AssetReferenceReport
{
	public string ExportVersion { get; set; } = string.Empty;

	/// <summary>
	/// True when an Addressables catalog was found in the exported project.
	/// </summary>
	public bool CatalogFound { get; set; }

	public int TotalCount { get; set; }
	public int UnityGuidCount { get; set; }
	public int CatalogKeyCount { get; set; }
	public int MissingCount { get; set; }

	public List<AssetReferenceEntry> References { get; set; } = new();
}

public sealed class AssetReferenceEntry
{
	/// <summary>
	/// The asset that contains the <c>m_AssetGUID</c> field, relative to the Unity project root.
	/// </summary>
	public string Path { get; set; } = string.Empty;

	public string Guid { get; set; } = string.Empty;

	/// <summary>
	/// <c>unity_guid</c>, <c>catalog_key</c> or <c>missing</c>.
	/// </summary>
	public string Kind { get; set; } = string.Empty;

	public int Occurrences { get; set; } = 1;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AssetReferenceReport))]
internal sealed partial class AssetReferenceReportSerializerContext : JsonSerializerContext
{
}
