namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// A texture whose exported file name had to be made unique (<c>name_0.png</c>).
/// </summary>
/// <remarks>
/// Plain text assets such as Spine <c>.atlas</c> files reference textures by name, so these
/// renames must be propagated after the export has finished.
/// </remarks>
public sealed class AssetRenameInfo
{
	public string AssetName { get; set; } = string.Empty;
	public int ClassID { get; set; }

	/// <summary>
	/// The name the asset would have had without the uniqueness suffix.
	/// </summary>
	public string OriginalFileName { get; set; } = string.Empty;

	/// <summary>
	/// The name the asset was actually exported as.
	/// </summary>
	public string ExportedFileName { get; set; } = string.Empty;

	/// <summary>
	/// The full output directory of the exported asset.
	/// </summary>
	public string Directory { get; set; } = string.Empty;

	/// <summary>
	/// The name of the source serialized file.
	/// </summary>
	public string? SourceFile { get; set; }
}
