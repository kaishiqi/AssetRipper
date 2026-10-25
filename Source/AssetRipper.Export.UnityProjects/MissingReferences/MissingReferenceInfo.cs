namespace AssetRipper.Export.UnityProjects.MissingReferences;

/// <summary>
/// A reference that could not be resolved during export.
/// </summary>
public sealed class MissingReferenceInfo
{
	/// <summary>
	/// The exported file that contains the reference, relative to the Unity project root.
	/// </summary>
	public string ReferrerPath { get; set; } = string.Empty;

	/// <summary>
	/// The name of the source serialized file that contains the referring asset.
	/// </summary>
	public string? ReferrerSourceFile { get; set; }

	public int ReferrerClassID { get; set; }
	public long ReferrerPathID { get; set; }
	public long ReferrerExportID { get; set; }

	/// <summary>
	/// <c>Asset</c> for an asset document or <c>Meta</c> for a .meta document.
	/// </summary>
	public string DocumentKind { get; set; } = "Asset";

	/// <summary>
	/// The original Unity field name, or the special target name for scene object identifiers.
	/// </summary>
	public string FieldPath { get; set; } = string.Empty;

	public int TargetClassID { get; set; }
	public long TargetPathID { get; set; }
	public int TargetFileID { get; set; }

	/// <summary>
	/// The name of the source serialized file that was supposed to contain the target.
	/// </summary>
	public string? TargetSourceFile { get; set; }

	public MissingReferenceReason Reason { get; set; }

	/// <summary>
	/// The synthetic file ID written into the YAML placeholder.
	/// </summary>
	public long PlaceholderFileID { get; set; }
}
