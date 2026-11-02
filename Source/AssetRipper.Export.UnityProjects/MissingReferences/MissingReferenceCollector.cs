namespace AssetRipper.Export.UnityProjects.MissingReferences;

/// <summary>
/// Collects missing references discovered while writing YAML.
/// </summary>
public sealed class MissingReferenceCollector
{
	/// <summary>
	/// Null pointers are extremely common, so the report keeps only the first
	/// <see cref="MaxNullInSourceReferences"/> distinct entries.
	/// </summary>
	public const int MaxNullInSourceReferences = 20000;

	private readonly List<MissingReferenceInfo> references = new();
	private readonly Dictionary<string, MissingReferenceInfo> referencesByKey = new(StringComparer.Ordinal);

	private readonly List<MissingReferenceInfo> nullInSourceReferences = new();
	private readonly Dictionary<string, MissingReferenceInfo> nullInSourceReferencesByKey = new(StringComparer.Ordinal);

	/// <summary>
	/// The number of distinct missing references.
	/// </summary>
	public int Count => references.Count;

	/// <summary>
	/// The total number of times missing references were written.
	/// </summary>
	public int TotalOccurrences { get; private set; }

	/// <summary>
	/// The number of distinct pointers that were already null in the source data.
	/// These are not missing references and are not included in <see cref="Count"/>.
	/// </summary>
	public int NullInSourceCount => nullInSourceReferences.Count;

	/// <summary>
	/// The total number of times null source pointers were written.
	/// </summary>
	public int NullInSourceOccurrences { get; private set; }

	/// <summary>
	/// True when <see cref="NullInSourceCount"/> reached <see cref="MaxNullInSourceReferences"/>.
	/// </summary>
	public bool NullInSourceTruncated { get; private set; }

	public void Clear()
	{
		references.Clear();
		referencesByKey.Clear();
		TotalOccurrences = 0;

		nullInSourceReferences.Clear();
		nullInSourceReferencesByKey.Clear();
		NullInSourceOccurrences = 0;
		NullInSourceTruncated = false;
	}

	public void Add(MissingReferenceInfo info)
	{
		TotalOccurrences++;
		AddOrIncrement(info, references, referencesByKey, int.MaxValue);
	}

	public void AddNullInSource(MissingReferenceInfo info)
	{
		NullInSourceOccurrences++;
		if (AddOrIncrement(info, nullInSourceReferences, nullInSourceReferencesByKey, MaxNullInSourceReferences) < 0)
		{
			NullInSourceTruncated = true;
		}
	}

	private static int AddOrIncrement(MissingReferenceInfo info, List<MissingReferenceInfo> list, Dictionary<string, MissingReferenceInfo> byKey, int limit)
	{
		string key = GetKey(info);
		if (byKey.TryGetValue(key, out MissingReferenceInfo? existing))
		{
			existing.Occurrences++;
			return 0;
		}

		if (list.Count >= limit)
		{
			return -1;
		}

		byKey.Add(key, info);
		list.Add(info);
		return 1;
	}

	private static string GetKey(MissingReferenceInfo info)
	{
		return string.Join('\0',
			info.ReferrerPath,
			info.DocumentKind,
			info.FieldPath,
			info.TargetClassID.ToString(),
			info.TargetFileID.ToString(),
			info.TargetPathID.ToString());
	}

	public IReadOnlyList<MissingReferenceInfo> GetSortedReferences()
	{
		return Sort(references);
	}

	public IReadOnlyList<MissingReferenceInfo> GetSortedNullInSourceReferences()
	{
		return Sort(nullInSourceReferences);
	}

	private static List<MissingReferenceInfo> Sort(List<MissingReferenceInfo> source)
	{
		return source
			.OrderBy(r => r.ReferrerPath, StringComparer.Ordinal)
			.ThenBy(r => r.FieldPath, StringComparer.Ordinal)
			.ThenBy(r => r.TargetClassID)
			.ThenBy(r => r.TargetPathID)
			.ToList();
	}
}
