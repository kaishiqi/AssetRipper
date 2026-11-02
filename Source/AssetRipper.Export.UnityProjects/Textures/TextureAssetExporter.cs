using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.TextureDecoder.Rgb.Formats;

namespace AssetRipper.Export.UnityProjects.Textures;

public class TextureAssetExporter : BinaryAssetExporter
{
	private const int DefaultPlaceholderSize = 2048;
	private const int MaxPlaceholderSize = 16384;

	public ImageExportFormat ImageExportFormat { get; }
	private SpriteExportMode SpriteExportMode { get; }
	public bool PreferOriginalTextureExtension { get; }
	public bool ExportTexturePlaceholders { get; }
	private bool ExportSprites => SpriteExportMode is not SpriteExportMode.Yaml;

	public TextureAssetExporter(FullConfiguration configuration)
	{
		ImageExportFormat = configuration.ExportSettings.ImageExportFormat;
		SpriteExportMode = configuration.ExportSettings.SpriteExportMode;
		PreferOriginalTextureExtension = configuration.ExportSettings.PreferOriginalTextureExtension;
		ExportTexturePlaceholders = configuration.ExportSettings.ExportTexturePlaceholders;
	}

	public static bool IsPlaceholder(ITexture2D texture)
	{
		return !texture.HasImageData() || texture.Width_C28 <= 0 || texture.Height_C28 <= 0;
	}

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset.MainAsset is SpriteInformationObject spriteInformationObject && (ExportSprites || asset is not ISprite))
		{
			exportCollection = new TextureExportCollection(this, spriteInformationObject, ExportSprites);
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		ITexture2D texture = (ITexture2D)asset;

		if (IsPlaceholder(texture))
		{
			if (!ExportTexturePlaceholders)
			{
				Logger.Log(LogType.Warning, LogCategory.Export, $"Can't export '{texture.Name}' because it has no image data");
				return false;
			}
			return ExportPlaceholder(texture, path, fileSystem);
		}

		if (!texture.CheckAssetIntegrity())
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Can't export '{texture.Name}' because resources file '{texture.StreamData_C28?.Path}' hasn't been found");
			return false;
		}

		if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, texture.GetTextureExportFormat(PreferOriginalTextureExtension, ImageExportFormat));
			return true;
		}
		else
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Unable to convert '{texture.Name}' to bitmap");
			return false;
		}
	}

	private bool ExportPlaceholder(ITexture2D texture, string path, FileSystem fileSystem)
	{
		int width = texture.Width_C28 > 0 ? texture.Width_C28 : DefaultPlaceholderSize;
		int height = texture.Height_C28 > 0 ? texture.Height_C28 : DefaultPlaceholderSize;
		width = Math.Clamp(width, 1, MaxPlaceholderSize);
		height = Math.Clamp(height, 1, MaxPlaceholderSize);

		DirectBitmap<ColorRGBA<byte>, byte> bitmap = new(width, height);
		using (Stream stream = fileSystem.File.Create(path))
		{
			bitmap.Save(stream, ImageExportFormat);
		}

		Logger.Log(LogType.Info, LogCategory.Export, $"Exported a {width}x{height} placeholder for '{texture.Name}' because it has no image data");
		return true;
	}
}
