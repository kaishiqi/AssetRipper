using AssetRipper.Import.Logging;

namespace AssetRipper.Export.Configuration;

public sealed record class ExportSettings
{
	/// <summary>
	/// The file format that audio clips get exported in. Recommended: Ogg
	/// </summary>
	public AudioExportFormat AudioExportFormat { get; set; } = AudioExportFormat.Default;

	/// <summary>
	/// The file format that images (like textures) get exported in.
	/// </summary>
	public ImageExportFormat ImageExportFormat { get; set; } = ImageExportFormat.Png;

	/// <summary>
	/// The file format that images (like textures) get exported in.
	/// </summary>
	public LightmapTextureExportFormat LightmapTextureExportFormat { get; set; } = LightmapTextureExportFormat.Yaml;

	/// <summary>
	/// How are MonoScripts exported? Recommended: Decompiled
	/// </summary>
	public ScriptExportMode ScriptExportMode { get; set; } = ScriptExportMode.Hybrid;

	/// <summary>
	/// The C# language version of decompiled scripts.
	/// </summary>
	public ScriptLanguageVersion ScriptLanguageVersion { get; set; } = ScriptLanguageVersion.AutoSafe;

	/// <summary>
	/// If true, type references in scripts are fully qualified.
	/// </summary>
	public bool ScriptTypesFullyQualified { get; set; } = false;

	/// <summary>
	/// How to export shaders?
	/// </summary>
	public ShaderExportMode ShaderExportMode { get; set; } = ShaderExportMode.Dummy;

	/// <summary>
	/// Should sprites be exported as a texture? Recommended: Native
	/// </summary>
	public SpriteExportMode SpriteExportMode { get; set; } = SpriteExportMode.Yaml;

	/// <summary>
	/// How are text assets exported?
	/// </summary>
	public TextExportMode TextExportMode { get; set; } = TextExportMode.Parse;

	public bool ExportUnreadableAssets { get; set; } = false;

	/// <summary>
	/// If true, the original texture extension (when available) will be preferred over the selected <see cref="ImageExportFormat"/>.
	/// </summary>
	public bool PreferOriginalTextureExtension { get; set; } = true;

	public bool SaveSettingsToDisk { get; set; }

	public string? LanguageCode { get; set; }

	/// <summary>
	/// The path to a previous export. When set, exported assets whose relative path matches an asset
	/// in this export reuse the guid from its .meta file. When empty, all guids are newly generated.
	/// </summary>
	public string? ReferenceProjectPath { get; set; }

	/// <summary>
	/// If true, the export fails after writing the missing reference report when any reference could not be resolved.
	/// </summary>
	public bool FailOnMissingReferences { get; set; } = false;

	/// <summary>
	/// If true, textures without image data (for example TMP dynamic font atlases) are exported as a
	/// placeholder PNG so that references to them remain valid.
	/// </summary>
	public bool ExportTexturePlaceholders { get; set; } = true;

	/// <summary>
	/// If true, SpriteAtlas assets are exported with a SpriteAtlasImporter instead of being unpacked
	/// into individual sprites. Unity re-packs the atlas on import.
	/// </summary>
	public bool PreserveSpriteAtlases { get; set; } = false;

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(AudioExportFormat)}: {AudioExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ImageExportFormat)}: {ImageExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(LightmapTextureExportFormat)}: {LightmapTextureExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptExportMode)}: {ScriptExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptLanguageVersion)}: {ScriptLanguageVersion}");
		Logger.Info(LogCategory.General, $"{nameof(ShaderExportMode)}: {ShaderExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(SpriteExportMode)}: {SpriteExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(TextExportMode)}: {TextExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ExportUnreadableAssets)}: {ExportUnreadableAssets}");
		Logger.Info(LogCategory.General, $"{nameof(PreferOriginalTextureExtension)}: {PreferOriginalTextureExtension}");
		Logger.Info(LogCategory.General, $"{nameof(ReferenceProjectPath)}: {ReferenceProjectPath}");
		Logger.Info(LogCategory.General, $"{nameof(FailOnMissingReferences)}: {FailOnMissingReferences}");
		Logger.Info(LogCategory.General, $"{nameof(ExportTexturePlaceholders)}: {ExportTexturePlaceholders}");
		Logger.Info(LogCategory.General, $"{nameof(PreserveSpriteAtlases)}: {PreserveSpriteAtlases}");
	}
}
