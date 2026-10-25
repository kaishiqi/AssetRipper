namespace AssetRipper.Export.UnityProjects.MissingReferences;

/// <summary>
/// Collects missing references discovered while writing YAML.
/// </summary>
public sealed class MissingReferenceCollector
{
	private readonly List<MissingReferenceInfo> references = new();
	private readonly Dictionary<string, MissingReferenceInfo> referencesByKey = new(StringComparer.Ordinal);

	/// <summary>
	/// The number of distinct missing references.
	/// </summary>
	public int Count => references.Count;

	/// <summary>
	/// The total number of times missing references were written.
	/// </summary>
	public int TotalOccurrences { get; private set; }

	public void Clear()
	{
		references.Clear();
		referencesByKey.Clear();
		TotalOccurrences = 0;
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

		TotalOccurrences++;
		if (referencesByKey.TryGetValue(key, out MissingReferenceInfo? existing))
		{
			existing.Occurrences++;
		}
		else
		{
			referencesByKey.Add(key, info);
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
