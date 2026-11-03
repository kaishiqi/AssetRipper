using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Scores typeless MonoBehaviours by how many serialized fields have default values.
/// </summary>
/// <remarks>
/// A typeless file can still parse correctly through assembly metadata. To find the ones that
/// did not, the default value ratio of each script is compared against the same script in files
/// that do have type trees. A high ratio on a typeless asset is a signal that the layout did not
/// match and the fields were silently read as zero.
/// </remarks>
public sealed class TypeTreeConfidenceReport
{
	public string ExportVersion { get; set; } = string.Empty;

	public int TypelessMonoBehaviourCount { get; set; }
	public int EvaluatedCount { get; set; }
	public int SuspiciousCount { get; set; }

	/// <summary>
	/// The average default value ratio across all type-tree MonoBehaviours.
	/// </summary>
	public double GlobalBaselineRatio { get; set; }

	public List<ConfidenceEntry> Entries { get; set; } = new();
	public List<ScriptBaseline> Scripts { get; set; } = new();
}

public sealed class ConfidenceEntry
{
	public string Name { get; set; } = string.Empty;
	public long PathID { get; set; }
	public string Collection { get; set; } = string.Empty;
	public string? Script { get; set; }

	public int FieldCount { get; set; }
	public int DefaultFieldCount { get; set; }
	public double DefaultRatio { get; set; }

	/// <summary>
	/// The average default ratio for this script in files with type trees.
	/// </summary>
	public double BaselineRatio { get; set; }

	public int BaselineSamples { get; set; }

	/// <summary>
	/// <see cref="DefaultRatio"/> minus <see cref="BaselineRatio"/>, clamped to zero.
	/// </summary>
	public double Suspicion { get; set; }

	public bool Suspicious { get; set; }
}

public sealed class ScriptBaseline
{
	public string Script { get; set; } = string.Empty;
	public int Samples { get; set; }
	public double DefaultRatio { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TypeTreeConfidenceReport))]
internal sealed partial class TypeTreeConfidenceReportSerializerContext : JsonSerializerContext
{
}
