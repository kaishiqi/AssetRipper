using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Shaders;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SerializedPass;
using AssetRipper.SourceGenerated.Subclasses.SerializedShader;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderState;
using AssetRipper.SourceGenerated.Subclasses.SerializedSubShader;

namespace AssetRipper.Tests;

internal class DummyShaderTextExporterTests
{
	[Test]
	public void DummyShaderKeepsRenderQueue()
	{
		string text = Export(CreateShader());

		Assert.That(text, Does.Contain("\"Queue\"=\"3100\""));
	}

	[Test]
	public void DummyShaderKeepsBlendState()
	{
		string text = Export(CreateShader());

		Assert.That(text, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
	}

	[Test]
	public void DummyShaderKeepsZWriteZTestCull()
	{
		string text = Export(CreateShader());

		using (Assert.EnterMultipleScope())
		{
			Assert.That(text, Does.Contain("ZWrite On"));
			Assert.That(text, Does.Contain("ZTest LEqual"));
			Assert.That(text, Does.Contain("Cull Back"));
		}
	}

	[Test]
	public void DummyShaderUsesNamedStateProperties()
	{
		IShader shader = CreateShader();
		ISerializedShaderState state = shader.ParsedForm!.SubShaders[0].Passes[0].State;
		state.RtBlend0.SourceBlend.Name = "_SrcBlend";
		state.RtBlend0.DestinationBlend.Name = "_DstBlend";
		state.ZWrite.Name = "_ZWrite";
		state.Culling.Name = "_Cull";

		string text = Export(shader);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(text, Does.Contain("Blend [_SrcBlend] [_DstBlend]"));
			Assert.That(text, Does.Contain("ZWrite [_ZWrite]"));
			Assert.That(text, Does.Contain("Cull [_Cull]"));
			Assert.That(text, Does.Contain("_SrcBlend (\"\", Float)"));
			Assert.That(text, Does.Contain("_ZWrite (\"\", Float)"));
			Assert.That(text, Does.Contain("_Cull (\"\", Float)"));
		}
	}

	[Test]
	public void DummyShaderOmitsDefaultStencilState()
	{
		string text = Export(CreateShader());

		Assert.That(text, Does.Not.Contain("Stencil"));
	}

	[Test]
	public void DummyShaderKeepsStencilState()
	{
		IShader shader = CreateShader();
		ISerializedShaderState state = shader.ParsedForm!.SubShaders[0].Passes[0].State;
		state.StencilRef.Value = 3;
		state.StencilOp.Comp.Value = 3;

		string text = Export(shader);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(text, Does.Contain("Stencil {"));
			Assert.That(text, Does.Contain("Ref 3"));
			Assert.That(text, Does.Contain("Comp Equal"));
		}
	}

	private static IShader CreateShader()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);
		IShader shader = collection.CreateShader();
		ISerializedShader parsedForm = shader.ParsedForm!;
		parsedForm.Name = "Test/Dummy";

		ISerializedSubShader subShader = parsedForm.SubShaders.AddNew();
		subShader.LOD = 200;
		subShader.Tags.Tags.Add("Queue", "3100");
		subShader.Tags.Tags.Add("RenderType", "Transparent");

		ISerializedPass pass = subShader.Passes.AddNew();
		pass.State.ZWrite.Value = 1;
		pass.State.ZTest.Value = 4;
		pass.State.Culling.Value = 2;
		pass.State.RtBlend0.SourceBlend.Value = 5;
		pass.State.RtBlend0.DestinationBlend.Value = 10;
		pass.State.RtBlend0.SourceBlendAlpha.Value = 5;
		pass.State.RtBlend0.DestinationBlendAlpha.Value = 10;
		return shader;
	}

	private static string Export(IShader shader)
	{
		using StringWriter writer = new();
		Assert.That(DummyShaderTextExporter.ExportShader(shader, writer), Is.True);
		return writer.ToString();
	}
}
