using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.MissingReferences;

[JsonConverter(typeof(JsonStringEnumConverter<MissingReferenceReason>))]
public enum MissingReferenceReason
{
	/// <summary>
	/// The target path ID does not exist in the source collection.
	/// </summary>
	AssetNotFound,

	/// <summary>
	/// The source dependency file for the target could not be resolved.
	/// </summary>
	MissingDependency,

	/// <summary>
	/// The target is a stripped/null object.
	/// </summary>
	NullObject,

	/// <summary>
	/// An asset exists at the target path ID, but it has a different type.
	/// </summary>
	TypeMismatch,

	/// <summary>
	/// The target was not placed in any export collection.
	/// </summary>
	NoExportCollection,

	/// <summary>
	/// The target was explicitly redirected as missing.
	/// </summary>
	RedirectedAsMissing,

	/// <summary>
	/// The target was deleted.
	/// </summary>
	Deleted,

	/// <summary>
	/// The target's exporter failed.
	/// </summary>
	ExportFailed,

	/// <summary>
	/// The target was deliberately skipped.
	/// </summary>
	Skipped,
}
