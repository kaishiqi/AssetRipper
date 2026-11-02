using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Compares the <c>m_AssetGUID</c> values in DataConfig assets with the guids of the exported
/// .meta files referenced by their <c>fileKey</c>.
/// </summary>
/// <remarks>
/// DataConfig stores guids from the original project. AssetRipper writes new guids, and the
/// reference folder keeps them stable across exports, but the config still points at the
/// original values. This report shows what a <c>fileKey</c> driven post-processing pass has to fix.
/// </remarks>
public sealed class DataConfigReport
{
	public string ExportVersion { get; set; } = string.Empty;

	public int TotalCount { get; set; }
	public int MatchCount { get; set; }

	/// <summary>
	/// The asset exists at the fileKey path, but the configured guid differs from the exported one.
	/// </summary>
	public int MismatchCount { get; set; }

	/// <summary>
	/// No asset was exported at the fileKey path.
	/// </summary>
	public int MissingAssetCount { get; set; }

	public List<DataConfigEntry> Problems { get; set; } = new();
}

public sealed class DataConfigEntry
{
	/// <summary>
	/// The DataConfig asset, relative to the Unity project root.
	/// </summary>
	public string ConfigPath { get; set; } = string.Empty;

	public string FileKey { get; set; } = string.Empty;
	public string ConfiguredGuid { get; set; } = string.Empty;

	/// <summary>
	/// The guid of the exported .meta file, or <see langword="null"/> when the asset is missing.
	/// </summary>
	public string? ActualGuid { get; set; }

	/// <summary>
	/// <c>guid_mismatch</c> or <c>asset_missing</c>.
	/// </summary>
	public string Kind { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DataConfigReport))]
internal sealed partial class DataConfigReportSerializerContext : JsonSerializerContext
{
}
