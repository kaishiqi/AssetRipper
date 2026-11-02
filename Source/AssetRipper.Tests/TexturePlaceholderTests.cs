using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

internal class TexturePlaceholderTests
{
	private const string PngPath = "/output/ExportedProject/Assets/Texture2D/Texture2D.png";
	private const string MetaPath = PngPath + ".meta";

	[Test]
	public void TextureWithoutImageDataGetsPlaceholder()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ITexture2D texture = collection.CreateTexture2D();
		new SpriteProcessor().Process(CreateGameData(collection));

		Export(fileSystem, collection);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists(PngPath), Is.True);
			Assert.That(fileSystem.File.ReadAllBytes(PngPath).Length, Is.GreaterThan(0));
			Assert.That(fileSystem.File.Exists(MetaPath), Is.True);
			Assert.That(texture.Width_C28, Is.EqualTo(0));
		}

		string meta = fileSystem.File.ReadAllText(MetaPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(meta, Does.Contain("isReadable: 1"));
			Assert.That(meta, Does.Contain("textureCompression: 0"));
		}
	}

	[Test]
	public void PlaceholderCanBeDisabled()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		collection.CreateTexture2D();
		new SpriteProcessor().Process(CreateGameData(collection));

		Export(fileSystem, collection, settings => settings.ExportSettings.ExportTexturePlaceholders = false);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists(PngPath), Is.False);
			Assert.That(fileSystem.File.Exists(MetaPath), Is.False);
		}
	}

	[Test]
	public void RenamedTextureIsReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		ITexture2D first = collection.CreateTexture2D();
		first.Name = "icon";
		ITexture2D second = collection.CreateTexture2D();
		second.Name = "icon";
		new SpriteProcessor().Process(CreateGameData(collection));

		Export(fileSystem, collection);

		string report = fileSystem.File.ReadAllText("/output/AuxiliaryFiles/renamed-textures.json");
		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.Exists("/output/ExportedProject/Assets/Texture2D/icon.png"), Is.True);
			Assert.That(fileSystem.File.Exists("/output/ExportedProject/Assets/Texture2D/icon_0.png"), Is.True);
			Assert.That(report, Does.Contain("\"OriginalFileName\": \"icon.png\""));
			Assert.That(report, Does.Contain("\"ExportedFileName\": \"icon_0.png\""));
		}
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
