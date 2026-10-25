namespace AssetRipper.Export.UnityProjects.MissingReferences;

/// <summary>
/// Collects missing references discovered while writing YAML.
/// </summary>
public sealed class MissingReferenceCollector
{
	private readonly List<MissingReferenceInfo> references = new();
	private readonly HashSet<string> keys = new(StringComparer.Ordinal);

	public int Count => references.Count;

	public void Clear()
	{
		references.Clear();
		keys.Clear();
	}

	public void Add(MissingReferenceInfo info)
	{
		string key = string.Join('\0',
			info.ReferrerPath,
			info.DocumentKind,
			info.FieldPath,
			info.TargetClassID.ToString(),
			info.TargetFileID.ToString(),
			info.TargetPathID.ToString());

		if (keys.Add(key))
		{
			references.Add(info);
		}
	}

	public IReadOnlyList<MissingReferenceInfo> GetSortedReferences()
	{
		return references
			.OrderBy(r => r.ReferrerPath, StringComparer.Ordinal)
			.ThenBy(r => r.FieldPath, StringComparer.Ordinal)
			.ThenBy(r => r.TargetClassID)
			.ThenBy(r => r.TargetPathID)
			.ToList();
	}
}
