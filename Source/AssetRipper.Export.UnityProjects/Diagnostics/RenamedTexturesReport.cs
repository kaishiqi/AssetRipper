using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

public sealed class RenamedTexturesReport
{
	public string ExportVersion { get; set; } = string.Empty;

	/// <summary>
	/// The number of textures whose exported file name had to be made unique.
	/// </summary>
	public int TotalRenameCount { get; set; }

	public List<TextRenameEntry> Renames { get; set; } = new();

	/// <summary>
	/// The number of plain text files whose references were rewritten.
	/// </summary>
	public int RepairedFileCount { get; set; }

	public List<TextReferenceRepair> Repairs { get; set; } = new();

	/// <summary>
	/// Page names in text files that could not be matched to an exported texture.
	/// </summary>
	public int UnresolvedCount { get; set; }

	public List<UnresolvedTextReference> Unresolved { get; set; } = new();
}

public sealed class TextRenameEntry
{
	public string AssetName { get; set; } = string.Empty;
	public int ClassID { get; set; }
	public string OriginalFileName { get; set; } = string.Empty;
	public string ExportedFileName { get; set; } = string.Empty;

	/// <summary>
	/// The export directory relative to the Unity project root.
	/// </summary>
	public string Directory { get; set; } = string.Empty;

	public string? SourceFile { get; set; }
}

public sealed class TextReferenceRepair
{
	/// <summary>
	/// The text file relative to the Unity project root.
	/// </summary>
	public string Path { get; set; } = string.Empty;
	public string OldName { get; set; } = string.Empty;
	public string NewName { get; set; } = string.Empty;
}

public sealed class UnresolvedTextReference
{
	public string Path { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(RenamedTexturesReport))]
internal sealed partial class RenamedTexturesSerializerContext : JsonSerializerContext
{
}
