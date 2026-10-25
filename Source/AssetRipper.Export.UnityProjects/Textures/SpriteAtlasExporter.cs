using AssetRipper.Assets;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.SourceGenerated.Classes.ClassID_1210832254;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Subclasses.SecondaryTextureSettings;
using AssetRipper.SourceGenerated.Subclasses.TextureImporterPlatformSettings;

namespace AssetRipper.Export.UnityProjects.Textures;

/// <summary>
/// Exports SpriteAtlas assets instead of unpacking them into individual sprites.
/// </summary>
public sealed class SpriteAtlasExporter : YamlExporterBase
{
	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is ISpriteAtlas atlas)
		{
			exportCollection = new SpriteAtlasExportCollection(this, atlas);
			return true;
		}
		exportCollection = null;
		return false;
	}
}

public sealed class SpriteAtlasExportCollection : AssetExportCollection<ISpriteAtlas>
{
	public SpriteAtlasExportCollection(IAssetExporter assetExporter, ISpriteAtlas asset) : base(assetExporter, asset)
	{
	}

	protected override IUnityObjectBase CreateImporter(IExportContainer container)
	{
		if (container.ExportVersion.GreaterThanOrEquals(2020, 1))
		{
			ISpriteAtlasImporter importer = SpriteAtlasImporter.Create(container.File, container.ExportVersion);
			importer.Name_R = Asset.Name_R;
			if (importer.Has_BindAsDefault())
			{
				importer.BindAsDefault = Asset.EditorData.BindAsDefault;
			}
			if (importer.Has_VariantMultiplier())
			{
				importer.VariantMultiplier = Asset.EditorData.VariantMultiplier;
			}
			if (importer.Has_TextureSettings() && importer.TextureSettings is not null)
			{
				importer.TextureSettings.CopyValues(Asset.EditorData.TextureSettings);
			}
			if (importer.Has_PackingSettings() && importer.PackingSettings is not null && Asset.EditorData.PackingSettings is not null)
			{
				importer.PackingSettings.CopyValues(Asset.EditorData.PackingSettings);
			}
			if (importer.Has_PlatformSettings() && importer.PlatformSettings is not null && Asset.EditorData.PlatformSettings is not null)
			{
				importer.PlatformSettings.Clear();
				foreach (ITextureImporterPlatformSettings settings in Asset.EditorData.PlatformSettings)
				{
					importer.PlatformSettings.AddNew().CopyValues(settings);
				}
			}
			if (importer.Has_SecondaryTextureSettings() && importer.SecondaryTextureSettings is not null && Asset.EditorData.SecondaryTextureSettings is not null)
			{
				importer.SecondaryTextureSettings.Clear();
				foreach (var pair in Asset.EditorData.SecondaryTextureSettings)
				{
					importer.SecondaryTextureSettings.Add(pair.Key, pair.Value.DeepClone());
				}
			}
			if (Asset.AssetBundleName is not null)
			{
				importer.AssetBundleName_R = Asset.AssetBundleName;
			}
			return importer;
		}

		return base.CreateImporter(container);
	}
}
