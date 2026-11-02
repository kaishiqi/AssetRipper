using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Extensions.Enums.Shader.SerializedShader;
using AssetRipper.SourceGenerated.Subclasses.SerializedPass;
using AssetRipper.SourceGenerated.Subclasses.SerializedProperties;
using AssetRipper.SourceGenerated.Subclasses.SerializedProperty;
using AssetRipper.SourceGenerated.Subclasses.SerializedShader;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderFloatValue;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderRTBlendState;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderState;
using AssetRipper.SourceGenerated.Subclasses.SerializedStencilOp;
using AssetRipper.SourceGenerated.Subclasses.SerializedSubShader;

namespace AssetRipper.Export.UnityProjects.Shaders;

public sealed class DummyShaderTextExporter : ShaderExporterBase
{
	// This uses CGPROGRAM instead of HLSLPROGRAM because the latter was supposedly introduced in Unity 5.6.
	// https://github.com/UnityCommunity/UnityReleaseNotes/blob/7b417b8ff64415e1e509d8c345b829c7cc11b650/5.6-Beta/5.6.0b1.txt#L143
	private static string FallbackDummyShader { get; } = """
			CGPROGRAM
		#pragma surface surf Lambert
		#pragma target 3.0
			sampler2D _MainTex;
			struct Input
			{
				float2 uv_MainTex;
			};
			void surf(Input IN, inout SurfaceOutput o)
			{
				float4 c = tex2D(_MainTex, IN.uv_MainTex);
				o.Albedo = c.rgb;
			}
			ENDCG

		""".Replace("\r", "");

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		return ExportShader((IShader)asset, path, fileSystem);
	}

	public static bool ExportShader(IShader shader, string path, FileSystem fileSystem)
	{
		using Stream fileStream = fileSystem.File.Create(path);
		using InvariantStreamWriter writer = new(fileStream);
		return ExportShader(shader, writer);
	}

	public static bool ExportShader(IShader shader, TextWriter writer)
	{
		// Technically, this outputs invalid shader code for Unity 5.5 because HLSLPROGRAM was not introduced until Unity 5.6.
		if (shader.Has_ParsedForm())
		{
			ISerializedShader parsedForm = shader.ParsedForm;
			ISerializedSubShader? subShader = parsedForm.SubShaders.Count > 0 ? parsedForm.SubShaders[0] : null;
			ISerializedPass? pass = subShader is not null && subShader.Passes.Count > 0 ? subShader.Passes[0] : null;
			ISerializedShaderState? state = pass?.State;

			writer.Write($"Shader \"{parsedForm.Name}\" {{\n");
			ExportProperties(parsedForm.PropInfo, state, writer);

			TemplateShader? templateShader = TemplateList.GetBestTemplate(shader);
			bool usesUiTemplate = templateShader?.TemplateName == "UI";

			writer.Write("\tSubShader{\n");
			WriteTags(subShader, pass, writer);
			// The UI template emits its own property driven stencil block.
			WriteRenderState(state, writer, !usesUiTemplate);

			// Keep the original pass count: Graphics.Blit and other APIs select passes by index,
			// and a shader with fewer passes makes those calls fail at runtime.
			int passCount = subShader is not null && subShader.Passes.Count > 0 ? subShader.Passes.Count : 1;
			for (int i = 0; i < passCount; i++)
			{
				ISerializedPass? currentPass = subShader is not null && i < subShader.Passes.Count ? subShader.Passes[i] : null;
				writer.Write("\t\t//DummyShaderTextExporter\n");
				if (templateShader != null)
				{
					writer.Write(ApplyPassName(templateShader.ShaderText, GetPassName(currentPass)));
				}
				else
				{
					writer.WriteIndent(2);
					writer.Write(FallbackDummyShader);
				}
				writer.Write('\n');
			}
			writer.Write("\t}\n");

			if (parsedForm.FallbackName != string.Empty)
			{
				writer.WriteIndent(1);
				writer.Write($"Fallback \"{parsedForm.FallbackName}\"\n");
			}
			if (parsedForm.CustomEditorName != string.Empty)
			{
				writer.WriteIndent(1);
				writer.Write($"//CustomEditor \"{parsedForm.CustomEditorName}\"\n");
			}
			writer.Write('}');
		}
		else
		{
			string header = shader.Script.String;
			int subshaderIndex = header.IndexOf("SubShader");
			if (subshaderIndex < 0)
			{
				return false;
			}
			writer.WriteString(header, 0, subshaderIndex);

			writer.Write("\tSubShader{\n");
			writer.Write("\t\t//DummyShaderTextExporter\n");
			writer.WriteIndent(2);
			writer.Write(FallbackDummyShader);
			writer.Write("\n\t}\n");
			writer.Write('}');
		}
		return true;
	}

	private static void WriteTags(ISerializedSubShader? subShader, ISerializedPass? pass, TextWriter writer)
	{
		string renderType = GetTag(subShader, pass, "RenderType") ?? "Opaque";
		string? queue = GetTag(subShader, pass, "Queue");

		writer.WriteIndent(2);
		if (queue is null)
		{
			writer.Write($"Tags {{ \"RenderType\"=\"{renderType}\" }}\n");
		}
		else
		{
			writer.Write($"Tags {{ \"Queue\"=\"{queue}\" \"RenderType\"=\"{renderType}\" }}\n");
		}

		if (subShader is not null)
		{
			writer.WriteIndent(2);
			writer.Write($"LOD {subShader.LOD}\n");
		}
	}

	private static string? GetTag(ISerializedSubShader? subShader, ISerializedPass? pass, string tagName)
	{
		if (subShader is not null && subShader.Tags.Tags.TryGetValue(tagName, out Utf8String? subShaderValue) && subShaderValue is not null)
		{
			return subShaderValue.String;
		}
		if (pass is not null && pass.Tags.Tags.TryGetValue(tagName, out Utf8String? passValue) && passValue is not null)
		{
			return passValue.String;
		}
		return null;
	}

	private static string? GetPassName(ISerializedPass? pass)
	{
		if (pass is null)
		{
			return null;
		}

		string name = pass.Name_R.String;
		return string.IsNullOrEmpty(name) ? null : name;
	}

	/// <summary>
	/// Insert a <c>Name</c> command into the template's <c>Pass</c> block.
	/// </summary>
	private static string ApplyPassName(string passText, string? passName)
	{
		if (string.IsNullOrEmpty(passName))
		{
			return passText;
		}

		int braceIndex = passText.IndexOf('{');
		return braceIndex < 0 ? passText : passText.Insert(braceIndex + 1, $"\n\t\t\tName \"{passName}\"");
	}

	private static void WriteRenderState(ISerializedShaderState? state, TextWriter writer, bool includeStencil)
	{
		if (state is null)
		{
			return;
		}

		WriteBlend(state.RtBlend0, writer);

		writer.WriteIndent(2);
		writer.Write($"ZWrite {GetStateValue(state.ZWrite, state.ZWriteValue)}\n");

		writer.WriteIndent(2);
		writer.Write($"ZTest {GetZTestValue(state.ZTest, state.ZTestValue)}\n");

		writer.WriteIndent(2);
		writer.Write($"Cull {GetStateValue(state.Culling, state.CullingValue)}\n");

		if (includeStencil)
		{
			WriteStencil(state, writer);
		}
	}

	private static void WriteBlend(ISerializedShaderRTBlendState blend, TextWriter writer)
	{
		string src = GetStateValue(blend.SourceBlend, blend.SrcBlendValue);
		string dst = GetStateValue(blend.DestinationBlend, blend.DestBlendValue);
		string srcAlpha = GetStateValue(blend.SourceBlendAlpha, blend.SrcBlendAlphaValue);
		string dstAlpha = GetStateValue(blend.DestinationBlendAlpha, blend.DestBlendAlphaValue);

		writer.WriteIndent(2);
		if (src == srcAlpha && dst == dstAlpha && !blend.SourceBlend.HasName && !blend.DestinationBlend.HasName
			&& !blend.SourceBlendAlpha.HasName && !blend.DestinationBlendAlpha.HasName)
		{
			// The alpha blend state matches the color blend state.
			writer.Write($"Blend {src} {dst}\n");
		}
		else
		{
			writer.Write($"Blend {src} {dst}, {srcAlpha} {dstAlpha}\n");
		}
	}

	private static void WriteStencil(ISerializedShaderState state, TextWriter writer)
	{
		if (IsStencilDefault(state))
		{
			return;
		}

		writer.WriteIndent(2);
		writer.Write("Stencil {\n");
		WriteStencilValue("Ref", state.StencilRef, writer);
		WriteStencilValue("ReadMask", state.StencilReadMask, writer);
		WriteStencilValue("WriteMask", state.StencilWriteMask, writer);
		WriteStencilOp(string.Empty, state.StencilOp, writer);
		WriteStencilOp("Front", state.StencilOpFront, writer);
		WriteStencilOp("Back", state.StencilOpBack, writer);
		writer.WriteIndent(2);
		writer.Write("}\n");
	}

	private static void WriteStencilValue(string name, ISerializedShaderFloatValue value, TextWriter writer)
	{
		writer.WriteIndent(3);
		writer.Write($"{name} {value.GetNameOrFloatString()}\n");
	}

	private static void WriteStencilOp(string suffix, ISerializedStencilOp stencilOp, TextWriter writer)
	{
		if (IsStencilOpDefault(stencilOp))
		{
			return;
		}

		writer.WriteIndent(3);
		writer.Write($"Comp{suffix} {GetStencilCompValue(stencilOp.Comp, stencilOp.CompValue)}\n");
		writer.WriteIndent(3);
		writer.Write($"Pass{suffix} {stencilOp.Pass.GetNameOrEnumString<StencilOp>()}\n");
		writer.WriteIndent(3);
		writer.Write($"Fail{suffix} {stencilOp.Fail.GetNameOrEnumString<StencilOp>()}\n");
		writer.WriteIndent(3);
		writer.Write($"ZFail{suffix} {stencilOp.ZFail.GetNameOrEnumString<StencilOp>()}\n");
	}

	private static string GetStencilCompValue(ISerializedShaderFloatValue value, StencilComp comp)
	{
		// Disabled is a serialized default, not a valid ShaderLab comparison.
		if (value.HasName)
		{
			return $"[{value.Name}]";
		}
		return (comp == StencilComp.Disabled ? StencilComp.Always : comp).ToString();
	}

	internal static bool IsStencilDefault(ISerializedShaderState state)
	{
		return IsStencilValueDefault(state.StencilRef, 0.0f)
			&& IsStencilValueDefault(state.StencilReadMask, 255.0f)
			&& IsStencilValueDefault(state.StencilWriteMask, 255.0f)
			&& IsStencilOpDefault(state.StencilOp)
			&& IsStencilOpDefault(state.StencilOpFront)
			&& IsStencilOpDefault(state.StencilOpBack);
	}

	private static bool IsStencilValueDefault(ISerializedShaderFloatValue value, float defaultValue)
	{
		// Zero is also treated as default because the generated assets use it for absent values.
		return !value.HasName && (value.Value == defaultValue || value.Value == 0.0f);
	}

	private static bool IsStencilOpDefault(ISerializedStencilOp stencilOp)
	{
		return (stencilOp.CompValue is StencilComp.Always or StencilComp.Disabled)
			&& stencilOp.PassValue == StencilOp.Keep
			&& stencilOp.FailValue == StencilOp.Keep
			&& stencilOp.ZFailValue == StencilOp.Keep;
	}

	private static string GetStateValue(ISerializedShaderFloatValue value, Enum literal)
	{
		return value.HasName ? $"[{value.Name}]" : literal.ToString();
	}

	private static string GetZTestValue(ISerializedShaderFloatValue value, ZTest literal)
	{
		// Disabled is a serialized default, not a valid ShaderLab comparison.
		return GetStateValue(value, literal == ZTest.Disabled ? ZTest.Always : literal);
	}

	private static void ExportProperties(ISerializedProperties properties, ISerializedShaderState? state, TextWriter writer)
	{
		writer.WriteIndent(1);
		writer.Write("Properties {\n");
		foreach (ISerializedProperty prop in properties.Props)
		{
			Export(prop, writer);
		}

		foreach ((string name, float defaultValue) in GetMissingStateProperties(properties, state))
		{
			writer.WriteIndent(2);
			writer.Write($"{name} (\"\", Float) = {defaultValue.ToStringInvariant()}\n");
		}

		writer.WriteIndent(1);
		writer.Write("}\n");
	}

	private static IEnumerable<(string Name, float DefaultValue)> GetMissingStateProperties(ISerializedProperties properties, ISerializedShaderState? state)
	{
		if (state is null)
		{
			yield break;
		}

		HashSet<string> existingNames = new(properties.Props.Select(p => p.Name.String), StringComparer.Ordinal);

		foreach ((string name, float value) in GetStatePropertyValues(state))
		{
			if (!existingNames.Contains(name))
			{
				yield return (name, value);
			}
		}
	}

	private static IEnumerable<(string Name, float Value)> GetStatePropertyValues(ISerializedShaderState state)
	{
		ISerializedShaderRTBlendState blend = state.RtBlend0;
		if (blend.SourceBlend.HasName)
		{
			yield return (blend.SourceBlend.Name.String, blend.SourceBlend.Value);
		}
		if (blend.DestinationBlend.HasName)
		{
			yield return (blend.DestinationBlend.Name.String, blend.DestinationBlend.Value);
		}
		if (blend.SourceBlendAlpha.HasName)
		{
			yield return (blend.SourceBlendAlpha.Name.String, blend.SourceBlendAlpha.Value);
		}
		if (blend.DestinationBlendAlpha.HasName)
		{
			yield return (blend.DestinationBlendAlpha.Name.String, blend.DestinationBlendAlpha.Value);
		}
		if (state.ZWrite.HasName)
		{
			yield return (state.ZWrite.Name.String, state.ZWrite.Value);
		}
		if (state.ZTest.HasName)
		{
			yield return (state.ZTest.Name.String, state.ZTest.Value);
		}
		if (state.Culling.HasName)
		{
			yield return (state.Culling.Name.String, state.Culling.Value);
		}
	}

	private static void Export(ISerializedProperty _this, TextWriter writer)
	{
		writer.WriteIndent(2);
		foreach (Utf8String attribute in _this.Attributes)
		{
			writer.Write($"[{attribute}] ");
		}
		SerializedPropertyFlag flags = (SerializedPropertyFlag)_this.Flags;
		if (flags.IsHideInInspector())
		{
			writer.Write("[HideInInspector] ");
		}
		if (flags.IsPerRendererData())
		{
			writer.Write("[PerRendererData] ");
		}
		if (flags.IsNoScaleOffset())
		{
			writer.Write("[NoScaleOffset] ");
		}
		if (flags.IsNormal())
		{
			writer.Write("[Normal] ");
		}
		if (flags.IsHDR())
		{
			writer.Write("[HDR] ");
		}
		if (flags.IsGamma())
		{
			writer.Write("[Gamma] ");
		}

		writer.Write($"{_this.Name} (\"{_this.Description}\", ");

		switch (_this.GetType_())
		{
			case SerializedPropertyType.Color:
			case SerializedPropertyType.Vector:
				writer.Write("Vector");
				break;

			case SerializedPropertyType.Float:
				writer.Write("Float");
				break;

			case SerializedPropertyType.Range:
				writer.Write($"Range({_this.DefValue_1_.ToStringInvariant()}, {_this.DefValue_2_.ToStringInvariant()})");
				break;

			case SerializedPropertyType.Texture:
				switch (_this.DefTexture.TexDim)
				{
					case 1:
						writer.Write("any");
						break;
					case 2:
						writer.Write("2D");
						break;
					case 3:
						writer.Write("3D");
						break;
					case 4:
						writer.Write("Cube");
						break;
					case 5:
						writer.Write("2DArray");
						break;
					case 6:
						writer.Write("CubeArray");
						break;
					default:
						throw new NotSupportedException("Texture dimension isn't supported");

				}
				break;

			case SerializedPropertyType.Int:
				writer.Write("Int");
				break;

			default:
				throw new NotSupportedException($"Serialized property type {_this.Type} isn't supported");
		}
		writer.Write(") = ");

		switch (_this.GetType_())
		{
			case SerializedPropertyType.Color:
			case SerializedPropertyType.Vector:
				writer.Write($"({_this.DefValue_0_.ToStringInvariant()},{_this.DefValue_1_.ToStringInvariant()},{_this.DefValue_2_.ToStringInvariant()},{_this.DefValue_3_.ToStringInvariant()})");
				break;

			case SerializedPropertyType.Float:
			case SerializedPropertyType.Range:
			case SerializedPropertyType.Int:
				writer.Write(_this.DefValue_0_.ToStringInvariant());
				break;

			case SerializedPropertyType.Texture:
				writer.Write($"\"{_this.DefTexture.DefaultName}\" {{}}");
				break;

			default:
				throw new NotSupportedException($"Serialized property type {_this.Type} isn't supported");
		}
		writer.Write('\n');
	}
}
