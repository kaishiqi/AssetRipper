using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Audio;

public sealed class AudioReport
{
	public string ExportMode { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public List<AudioReportEntry> Clips { get; set; } = new();
}

public sealed class AudioReportEntry
{
	public string Name { get; set; } = string.Empty;
	public long PathID { get; set; }
	public string Collection { get; set; } = string.Empty;

	/// <summary>
	/// The compression declared by the clip, from <c>m_Type</c> or <c>m_CompressionFormat</c>.
	/// </summary>
	public string? DeclaredCompression { get; set; }

	/// <summary>
	/// The detected source container, for example <c>fsb</c>, <c>it</c>, or <c>ogg</c>.
	/// </summary>
	public string? Container { get; set; }

	/// <summary>
	/// The codec reported by the source container.
	/// </summary>
	public string? Codec { get; set; }

	public bool CanDecode { get; set; }

	/// <summary>
	/// The file extension that the current export mode produces.
	/// </summary>
	public string? ExportedExtension { get; set; }

	public int InlineDataLength { get; set; }
	public string? StreamSource { get; set; }
	public ulong StreamSize { get; set; }
	public string? Message { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AudioReport))]
internal sealed partial class AudioReportSerializerContext : JsonSerializerContext
{
}
