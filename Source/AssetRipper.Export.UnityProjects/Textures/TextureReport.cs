using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>
/// Lists textures that were exported as placeholders because they had no image data or zero size.
/// </summary>
public sealed class TextureReport
{
	public string ExportVersion { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public int PlaceholderCount { get; set; }
	public List<TextureReportEntry> Placeholders { get; set; } = new();
}

public sealed class TextureReportEntry
{
	public string Name { get; set; } = string.Empty;
	public long PathID { get; set; }
	public string Collection { get; set; } = string.Empty;
	public int Width { get; set; }
	public int Height { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TextureReport))]
internal sealed partial class TextureReportSerializerContext : JsonSerializerContext
{
}
