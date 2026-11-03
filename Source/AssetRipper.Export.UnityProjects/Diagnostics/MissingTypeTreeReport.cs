using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Describes serialized files without type trees and the MonoBehaviours whose fields
/// could not be parsed from assembly metadata.
/// </summary>
/// <remarks>
/// When a bundle has no type tree, AssetRipper reconstructs a MonoBehaviour from the managed
/// assembly. Field layout mismatches make PPtrs and scalars silently read as zero. This report
/// lists the affected files and scripts so post-processing can target them.
/// </remarks>
public sealed class MissingTypeTreeReport
{
	public string ExportVersion { get; set; } = string.Empty;

	public int TypelessCollectionCount { get; set; }
	public int TypelessMonoBehaviourCount { get; set; }

	/// <summary>
	/// MonoBehaviours whose structure failed to load even though their file has type trees.
	/// </summary>
	public int ParseFailureCount { get; set; }

	public List<TypelessCollectionEntry> Collections { get; set; } = new();
	public List<ScriptSummary> Scripts { get; set; } = new();

	public List<MonoBehaviourSample> ParseFailures { get; set; } = new();
}

public sealed class TypelessCollectionEntry
{
	public string Collection { get; set; } = string.Empty;
	public int MonoBehaviourCount { get; set; }
	public int FailedCount { get; set; }

	/// <summary>
	/// A bounded sample; counts above cover the whole collection.
	/// </summary>
	public List<MonoBehaviourSample> Samples { get; set; } = new();
}

public sealed class MonoBehaviourSample
{
	public string Name { get; set; } = string.Empty;
	public long PathID { get; set; }
	public string? Script { get; set; }
	public string? Assembly { get; set; }

	/// <summary>
	/// <c>Loaded</c>, <c>Unloaded</c> or <c>Failed</c>.
	/// </summary>
	public string Structure { get; set; } = string.Empty;

	public int FieldCount { get; set; }
}

public sealed class ScriptSummary
{
	public string Script { get; set; } = string.Empty;
	public int Count { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MissingTypeTreeReport))]
internal sealed partial class MissingTypeTreeReportSerializerContext : JsonSerializerContext
{
}
