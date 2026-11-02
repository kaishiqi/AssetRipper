using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Shaders;

public sealed class ShaderReport
{
	public string ExportVersion { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public int ParsedCount { get; set; }
	public int UnparsedCount { get; set; }

	/// <summary>
	/// The number of shaders whose first subshader has more than one pass.
	/// </summary>
	public int MultiPassCount { get; set; }

	public int DuplicateNameCount { get; set; }
	public List<ShaderReportEntry> Shaders { get; set; } = new();
	public List<DuplicateShaderName> DuplicateNames { get; set; } = new();
}

public sealed class ShaderReportEntry
{
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// The name declared inside the shader, or <see langword="null"/> when the shader is unparsed.
	/// </summary>
	public string? ShaderName { get; set; }

	public long PathID { get; set; }
	public string Collection { get; set; } = string.Empty;
	public bool HasParsedForm { get; set; }
	public int SubShaderCount { get; set; }
	public int PassCount { get; set; }
	public int PropertyCount { get; set; }
	public string? Queue { get; set; }
	public string? RenderType { get; set; }
	public string? Blend { get; set; }
	public string? ZWrite { get; set; }
	public string? ZTest { get; set; }
	public string? Cull { get; set; }
	public bool HasCustomStencil { get; set; }
}

public sealed class DuplicateShaderName
{
	public string Name { get; set; } = string.Empty;
	public int Count { get; set; }
	public List<ShaderReportLocation> Shaders { get; set; } = new();
}

public sealed class ShaderReportLocation
{
	public string AssetName { get; set; } = string.Empty;
	public string Collection { get; set; } = string.Empty;
	public long PathID { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ShaderReport))]
internal sealed partial class ShaderReportSerializerContext : JsonSerializerContext
{
}
