using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class SpriteAtlasPreserveTests
{
	private const string AtlasPath = "/output/ExportedProject/Assets/SpriteAtlas/TestAtlas.spriteatlas";

	[Test]
	public void PreserveExportsSpriteAtlas()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ISpriteAtlas atlas = collection.CreateSpriteAtlas();
		atlas.Name = "TestAtlas";
		atlas.ConvertToEditorFormat();

		Export(fileSystem, collection, settings => settings.ExportSettings.PreserveSpriteAtlases = true);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists(AtlasPath), Is.True);
			Assert.That(fileSystem.File.Exists(AtlasPath + ".meta"), Is.True);
			Assert.That(fileSystem.File.ReadAllText(AtlasPath + ".meta"), Does.Contain("SpriteAtlasImporter"));
		}
	}

	[Test]
	public void UnpackDoesNotExportSpriteAtlas()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ISpriteAtlas atlas = collection.CreateSpriteAtlas();
		atlas.Name = "TestAtlas";

		Export(fileSystem, collection);

		Assert.That(fileSystem.File.Exists(AtlasPath), Is.False);
	}

	private static void Export(VirtualFileSystem fileSystem, ProcessedAssetCollection collection, Action<FullConfiguration>? configure = null)
	{
		FullConfiguration settings = new();
		configure?.Invoke(settings);
		new ExportHandler(settings).Export(CreateGameData(collection), "/output", fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
