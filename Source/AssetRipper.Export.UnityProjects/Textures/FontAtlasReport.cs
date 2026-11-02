using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Textures;

public sealed class FontAtlasReport
{
	public string ExportVersion { get; set; } = string.Empty;

	/// <summary>
	/// The number of TMP font assets with <c>m_AtlasPopulationMode: 1</c>.
	/// </summary>
	public int FontAssetCount { get; set; }

	public int AtlasTextureCount { get; set; }

	/// <summary>
	/// The number of .meta files that were rewritten.
	/// </summary>
	public int ModifiedMetaCount { get; set; }

	/// <summary>
	/// The number of referenced atlas textures that were not exported.
	/// </summary>
	public int MissingAtlasCount { get; set; }

	public List<FontAtlasEntry> FontAssets { get; set; } = new();
}

public sealed class FontAtlasEntry
{
	public string FontAssetPath { get; set; } = string.Empty;
	public List<FontAtlasTexture> AtlasTextures { get; set; } = new();
}

public sealed class FontAtlasTexture
{
	public string Guid { get; set; } = string.Empty;

	/// <summary>
	/// The .meta file relative to the Unity project root, or <see langword="null"/> when the
	/// texture was not exported.
	/// </summary>
	public string? MetaPath { get; set; }

	public bool Modified { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(FontAtlasReport))]
internal sealed partial class FontAtlasReportSerializerContext : JsonSerializerContext
{
}
