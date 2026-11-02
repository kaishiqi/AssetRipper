namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Collects the texture renames that happen while exporting.
/// </summary>
public sealed class AssetRenameCollector
{
	private readonly List<AssetRenameInfo> items = new();

	public IReadOnlyList<AssetRenameInfo> Items => items;

	public int Count => items.Count;

	public void Clear()
	{
		items.Clear();
	}

	public void Add(AssetRenameInfo info)
	{
		ArgumentNullException.ThrowIfNull(info);
		items.Add(info);
	}
}
