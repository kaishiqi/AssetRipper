using System.Text.Json.Serialization;

namespace AssetRipper.Export.UnityProjects.Addressables;

public sealed class AddressablesReport
{
	public string? SourceDirectory { get; set; }
	public string DestinationDirectory { get; set; } = string.Empty;
	public List<string> CopiedFiles { get; set; } = new();
	public bool HasCatalog { get; set; }
	public bool HasSettings { get; set; }
	public string? Note { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AddressablesReport))]
internal sealed partial class AddressablesReportSerializerContext : JsonSerializerContext
{
}
