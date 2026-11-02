using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Textures;
using AssetRipper.IO.Files;

namespace AssetRipper.Tests;

internal class DynamicFontAtlasPostProcessorTests
{
	private const string AssetsPath = "/output/ExportedProject/Assets";
	private const string FontDirectory = AssetsPath + "/Font";
	private const string FontAssetPath = FontDirectory + "/MyFont.asset";
	private const string AtlasMetaPath = FontDirectory + "/Atlas.png.meta";
	private const string ReportPath = "/output/AuxiliaryFiles/font-atlas-report.json";
	private const string AtlasGuid = "0123456789abcdef0123456789abcdef";

	[Test]
	public void DynamicFontAtlasMetaIsAdjusted()
	{
		VirtualFileSystem fileSystem = new();
		CreateFontAsset(fileSystem);
		fileSystem.File.WriteAllText(AtlasMetaPath, CreateTextureMeta(isReadable: 0, textureType: 8, compression: 1, srgb: 1, alphaIsTransparency: 0));

		DynamicFontAtlasPostProcessor.Process(CreateSettings(), fileSystem);

		string meta = fileSystem.File.ReadAllText(AtlasMetaPath);
		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(meta, Does.Contain("textureType: 10"));
			Assert.That(meta, Does.Contain("textureCompression: 0"));
			Assert.That(meta, Does.Contain("isReadable: 1"));
			Assert.That(meta, Does.Contain("sRGBTexture: 0"));
			Assert.That(meta, Does.Contain("alphaIsTransparency: 1"));
			Assert.That(json, Does.Contain("\"ModifiedMetaCount\": 1"));
			Assert.That(json, Does.Contain("\"MissingAtlasCount\": 0"));
			Assert.That(json, Does.Contain("Assets/Font/MyFont.asset"));
		}
	}

	[Test]
	public void MissingAtlasIsReported()
	{
		VirtualFileSystem fileSystem = new();
		CreateFontAsset(fileSystem);

		DynamicFontAtlasPostProcessor.Process(CreateSettings(), fileSystem);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"MissingAtlasCount\": 1"));
			Assert.That(json, Does.Contain(AtlasGuid));
		}
	}

	[Test]
	public void StaticFontAssetIsIgnored()
	{
		VirtualFileSystem fileSystem = new();
		fileSystem.Directory.Create(FontDirectory);
		fileSystem.File.WriteAllText(FontAssetPath, "MonoBehaviour:\n  m_AtlasPopulationMode: 0\n");
		fileSystem.File.WriteAllText(AtlasMetaPath, CreateTextureMeta(isReadable: 0, textureType: 8, compression: 1, srgb: 1, alphaIsTransparency: 0));

		DynamicFontAtlasPostProcessor.Process(CreateSettings(), fileSystem);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(fileSystem.File.ReadAllText(AtlasMetaPath), Does.Contain("textureCompression: 1"));
			Assert.That(fileSystem.File.ReadAllText(ReportPath), Does.Contain("\"FontAssetCount\": 0"));
		}
	}

	private static void CreateFontAsset(VirtualFileSystem fileSystem)
	{
		fileSystem.Directory.Create(FontDirectory);
		string text = string.Concat(
			"MonoBehaviour:\n",
			"  m_AtlasPopulationMode: 1\n",
			"  m_AtlasTextures:\n",
			"  - {fileID: 2800000, guid: ", AtlasGuid, ", type: 3}\n",
			"  m_AtlasTextureIndex: 0\n");
		fileSystem.File.WriteAllText(FontAssetPath, text);
	}

	private static string CreateTextureMeta(int isReadable, int textureType, int compression, int srgb, int alphaIsTransparency)
	{
		return $"""
			fileFormatVersion: 2
			guid: {AtlasGuid}
			TextureImporter:
			  internalIDToNameTable: []
			  isReadable: {isReadable}
			  textureType: {textureType}
			  textureCompression: {compression}
			  sRGBTexture: {srgb}
			  alphaIsTransparency: {alphaIsTransparency}
			""";
	}

	private static FullConfiguration CreateSettings()
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		return settings;
	}
}
