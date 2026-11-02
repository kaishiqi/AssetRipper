using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Shaders;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SerializedPass;
using AssetRipper.SourceGenerated.Subclasses.SerializedSubShader;

namespace AssetRipper.Tests;

internal class ShaderReportTests
{
	private const string ReportPath = "/output/AuxiliaryFiles/shader-report.json";

	[Test]
	public void ReportDescribesPassStructure()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		IShader shader = collection.CreateShader();
		shader.ParsedForm!.Name = "Test/Dummy";
		ISerializedSubShader subShader = shader.ParsedForm.SubShaders.AddNew();
		subShader.Tags.Tags.Add("Queue", "3000");
		subShader.Tags.Tags.Add("RenderType", "Transparent");
		ISerializedPass firstPass = subShader.Passes.AddNew();
		firstPass.State.StencilRef.Value = 3;
		subShader.Passes.AddNew();

		WriteReport(fileSystem, collection);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"TotalCount\": 1"));
			Assert.That(json, Does.Contain("\"ShaderName\": \"Test/Dummy\""));
			Assert.That(json, Does.Contain("\"PassCount\": 2"));
			Assert.That(json, Does.Contain("\"MultiPassCount\": 1"));
			Assert.That(json, Does.Contain("\"Queue\": \"3000\""));
			Assert.That(json, Does.Contain("\"HasCustomStencil\": true"));
		}
	}

	[Test]
	public void DuplicateShaderNamesAreReported()
	{
		VirtualFileSystem fileSystem = new();
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		CreateNamedShader(collection, "Test/Duplicate");
		CreateNamedShader(collection, "Test/Duplicate");

		WriteReport(fileSystem, collection);

		string json = fileSystem.File.ReadAllText(ReportPath);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(json, Does.Contain("\"DuplicateNameCount\": 1"));
			Assert.That(json, Does.Contain("\"Name\": \"Test/Duplicate\""));
			Assert.That(json, Does.Contain("\"Count\": 2"));
		}
	}

	private static void CreateNamedShader(ProcessedAssetCollection collection, string name)
	{
		IShader shader = collection.CreateShader();
		shader.ParsedForm!.Name = name;
		ISerializedSubShader subShader = shader.ParsedForm.SubShaders.AddNew();
		subShader.Passes.AddNew();
	}

	private static void WriteReport(VirtualFileSystem fileSystem, ProcessedAssetCollection collection)
	{
		FullConfiguration settings = new();
		settings.ExportRootPath = "/output";
		new ShaderReportPostExporter().DoPostExport(CreateGameData(collection), settings, fileSystem);
	}

	private static GameData CreateGameData(ProcessedAssetCollection collection)
	{
		return new((GameBundle)collection.Bundle, collection.Version, new BaseManager(_ => { }), null);
	}
}
