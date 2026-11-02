using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SerializedPass;
using AssetRipper.SourceGenerated.Subclasses.SerializedShaderFloatValue;
using AssetRipper.SourceGenerated.Subclasses.SerializedSubShader;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Shaders;

/// <summary>
/// Writes a report describing the structure of every exported shader.
/// </summary>
/// <remarks>
/// Unity APIs such as <c>Graphics.Blit</c> select passes by index, and some framework shaders
/// rely on <c>Shader.Find</c>. The report exposes pass counts, render state and duplicate
/// shader names so that framework shaders can be replaced by their real counterparts.
/// </remarks>
public sealed class ShaderReportPostExporter : IPostExporter
{
	public const string FileName = "shader-report.json";

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		List<ShaderReportEntry> entries = new();
		foreach (var collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (IUnityObjectBase asset in collection)
			{
				if (asset is IShader shader)
				{
					entries.Add(CreateEntry(collection.Name, shader));
				}
			}
		}

		entries.Sort((a, b) =>
		{
			int result = string.CompareOrdinal(a.ShaderName ?? a.Name, b.ShaderName ?? b.Name);
			if (result != 0)
			{
				return result;
			}
			result = string.CompareOrdinal(a.Collection, b.Collection);
			return result != 0 ? result : a.PathID.CompareTo(b.PathID);
		});

		List<DuplicateShaderName> duplicates = new();
		foreach (IGrouping<string, ShaderReportEntry> group in entries
			.Where(e => !string.IsNullOrEmpty(e.ShaderName))
			.GroupBy(e => e.ShaderName!, StringComparer.Ordinal)
			.Where(g => g.Count() > 1)
			.OrderBy(g => g.Key, StringComparer.Ordinal))
		{
			DuplicateShaderName duplicate = new()
			{
				Name = group.Key,
				Count = group.Count(),
			};
			foreach (ShaderReportEntry entry in group)
			{
				duplicate.Shaders.Add(new ShaderReportLocation
				{
					AssetName = entry.Name,
					Collection = entry.Collection,
					PathID = entry.PathID,
				});
			}
			duplicates.Add(duplicate);
		}

		ShaderReport report = new()
		{
			ExportVersion = settings.Version.ToString(),
			TotalCount = entries.Count,
			ParsedCount = entries.Count(e => e.HasParsedForm),
			UnparsedCount = entries.Count(e => !e.HasParsedForm),
			MultiPassCount = entries.Count(e => e.PassCount > 1),
			DuplicateNameCount = duplicates.Count,
			Shaders = entries,
			DuplicateNames = duplicates,
		};

		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, ShaderReportSerializerContext.Default.ShaderReport);
		stream.Flush();

		if (report.MultiPassCount > 0)
		{
			Logger.Info(LogCategory.Export, $"{report.MultiPassCount} shaders have multiple passes. See '{FileName}'.");
		}
		if (report.DuplicateNameCount > 0)
		{
			Logger.Warning(LogCategory.Export, $"{report.DuplicateNameCount} shader names are exported more than once. See '{FileName}'.");
		}
	}

	private static ShaderReportEntry CreateEntry(string collectionName, IShader shader)
	{
		ShaderReportEntry entry = new()
		{
			Name = shader.GetBestName(),
			PathID = shader.PathID,
			Collection = collectionName,
			HasParsedForm = shader.Has_ParsedForm(),
		};

		if (!shader.Has_ParsedForm())
		{
			return entry;
		}

		var parsedForm = shader.ParsedForm;
		entry.ShaderName = parsedForm.Name.String;
		entry.SubShaderCount = parsedForm.SubShaders.Count;
		entry.PropertyCount = parsedForm.PropInfo.Props.Count;

		if (parsedForm.SubShaders.Count == 0)
		{
			return entry;
		}

		ISerializedSubShader subShader = parsedForm.SubShaders[0];
		entry.PassCount = subShader.Passes.Count;
		entry.Queue = GetTag(subShader, "Queue");
		entry.RenderType = GetTag(subShader, "RenderType");

		ISerializedPass? pass = subShader.Passes.Count > 0 ? subShader.Passes[0] : null;
		if (pass is null)
		{
			return entry;
		}

		if (entry.Queue is null)
		{
			entry.Queue = GetTag(pass, "Queue");
		}
		if (entry.RenderType is null)
		{
			entry.RenderType = GetTag(pass, "RenderType");
		}

		var state = pass.State;
		entry.Blend = $"{GetValue(state.RtBlend0.SourceBlend, state.RtBlend0.SrcBlendValue)} {GetValue(state.RtBlend0.DestinationBlend, state.RtBlend0.DestBlendValue)}";
		entry.ZWrite = GetValue(state.ZWrite, state.ZWriteValue);
		entry.ZTest = GetValue(state.ZTest, state.ZTestValue);
		entry.Cull = GetValue(state.Culling, state.CullingValue);
		entry.HasCustomStencil = !DummyShaderTextExporter.IsStencilDefault(state);
		return entry;
	}

	private static string? GetTag(ISerializedSubShader subShader, string tagName)
	{
		return subShader.Tags.Tags.TryGetValue(tagName, out Utf8String? value) ? value?.String : null;
	}

	private static string? GetTag(ISerializedPass pass, string tagName)
	{
		return pass.Tags.Tags.TryGetValue(tagName, out Utf8String? value) ? value?.String : null;
	}

	private static string GetValue(ISerializedShaderFloatValue value, Enum literal)
	{
		return value.HasName ? $"[{value.Name.String}]" : literal.ToString();
	}
}
