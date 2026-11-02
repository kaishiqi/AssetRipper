namespace AssetRipper.Export.UnityProjects.MissingReferences;

/// <summary>
/// Collects missing references discovered while writing YAML.
/// </summary>
public sealed class MissingReferenceCollector
{
	/// <summary>
	/// The default number of distinct null source pointers kept in the report.
	/// Override with the <c>ASSETRIPPER_NULL_IN_SOURCE_LIMIT</c> environment variable.
	/// </summary>
	public const int DefaultNullInSourceLimit = 20000;

	/// <summary>
	/// The number of distinct null source pointers kept in the report. Zero disables the list.
	/// </summary>
	public int NullInSourceLimit { get; set; } = GetDefaultNullInSourceLimit();

	private readonly List<MissingReferenceInfo> references = new();
	private readonly Dictionary<ReferenceKey, MissingReferenceInfo> referencesByKey = new();

	private readonly List<MissingReferenceInfo> nullInSourceReferences = new();
	private readonly Dictionary<ReferenceKey, MissingReferenceInfo> nullInSourceReferencesByKey = new();

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
	/// True when <see cref="NullInSourceCount"/> reached <see cref="NullInSourceLimit"/>.
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
		if (AddOrIncrement(info, nullInSourceReferences, nullInSourceReferencesByKey, NullInSourceLimit) < 0)
		{
			NullInSourceTruncated = true;
		}
	}

	private static int GetDefaultNullInSourceLimit()
	{
		string? value = Environment.GetEnvironmentVariable("ASSETRIPPER_NULL_IN_SOURCE_LIMIT");
		return int.TryParse(value, out int limit) && limit >= 0 ? limit : DefaultNullInSourceLimit;
	}

	private static int AddOrIncrement(MissingReferenceInfo info, List<MissingReferenceInfo> list, Dictionary<ReferenceKey, MissingReferenceInfo> byKey, int limit)
	{
		ReferenceKey key = new(
			info.ReferrerPath,
			info.DocumentKind,
			info.FieldPath,
			info.TargetClassID,
			info.TargetFileID,
			info.TargetPathID);

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

	private readonly record struct ReferenceKey(
		string ReferrerPath,
		string DocumentKind,
		string FieldPath,
		int TargetClassID,
		int TargetFileID,
		long TargetPathID);

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
