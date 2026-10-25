using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.UnityProjects.MissingReferences;
using AssetRipper.SourceGenerated.Subclasses.SceneObjectIdentifier;
using AssetRipper.Yaml;

namespace AssetRipper.Export.UnityProjects.Project;

public sealed class ProjectYamlWalker : YamlWalker
{
	private readonly IExportContainer container;
	private readonly ProjectAssetContainer? projectContainer;
	private readonly MissingReferenceCollector? missingReferences;
	private readonly Stack<string> fieldStack = new();

	public ProjectYamlWalker(IExportContainer container)
	{
		this.container = container;
		if (container is ProjectAssetContainer projectContainer)
		{
			this.projectContainer = projectContainer;
			missingReferences = projectContainer.MissingReferences;
		}
		WithUnityVersion(container.ExportVersion);
	}

	public IUnityObjectBase CurrentAsset { get; set; } = null!;

	/// <summary>
	/// The path of the file currently being written, used for missing reference reports.
	/// </summary>
	public string? DocumentPath { get; set; }

	/// <summary>
	/// True when this walker is writing an asset importer inside a .meta file.
	/// </summary>
	public bool IsMetaDocument { get; set; }

	public YamlDocument ExportYamlDocument(IUnityObjectBase asset)
	{
		CurrentAsset = asset;
		return ExportYamlDocument(asset, container.GetExportID(asset));
	}

	public YamlNode ExportYamlNode(IUnityObjectBase asset)
	{
		CurrentAsset = asset;
		return base.ExportYamlNode(asset);
	}

	public override bool EnterField(IUnityAssetBase asset, string name)
	{
		if (base.EnterField(asset, name))
		{
			fieldStack.Push(name);
			return true;
		}
		return false;
	}

	public override void ExitField(IUnityAssetBase asset, string name)
	{
		base.ExitField(asset, name);
		if (fieldStack.Count > 0 && fieldStack.Peek() == name)
		{
			fieldStack.Pop();
		}
	}

	public override bool EnterAsset(IUnityAssetBase asset)
	{
		if (asset is SceneObjectIdentifier sceneObjectIdentifier)
		{
			long targetObject;
			if (sceneObjectIdentifier.TargetObjectReference is not null)
			{
				MetaPtr pointer = container.CreateExportPointer(sceneObjectIdentifier.TargetObjectReference);
				targetObject = pointer.FileID;
				ReportMissingSceneObjectReference(sceneObjectIdentifier.TargetObjectReference, "targetObject", pointer);
			}
			else
			{
				targetObject = sceneObjectIdentifier.TargetObject;
			}

			long targetPrefab;
			if (sceneObjectIdentifier.TargetPrefabReference is not null)
			{
				MetaPtr pointer = container.CreateExportPointer(sceneObjectIdentifier.TargetPrefabReference);
				targetPrefab = pointer.FileID;
				ReportMissingSceneObjectReference(sceneObjectIdentifier.TargetPrefabReference, "targetPrefab", pointer);
			}
			else
			{
				targetPrefab = sceneObjectIdentifier.TargetPrefab;
			}

			YamlMappingNode yamlMappingNode = new()
			{
				{ YamlScalarNode.Create("targetObject"), targetObject },
				{ YamlScalarNode.Create("targetPrefab"), targetPrefab },
			};
			AddNode(yamlMappingNode);
			return false;
		}
		else
		{
			return base.EnterAsset(asset);
		}
	}

	public override YamlNode CreateYamlNodeForPPtr<TAsset>(PPtr<TAsset> pptr)
	{
		if (pptr.PathID == 0)
		{
			return MetaPtr.NullPtr.ExportYaml(container.ExportVersion);
		}
		else if (CurrentAsset.Collection.TryGetAsset(pptr, out TAsset? asset))
		{
			MetaPtr pointer = container.CreateExportPointer(asset);
			if (pointer.GUID == UnityGuid.MissingReference)
			{
				MissingReferenceReason reason = projectContainer?.GetMissingReferenceReason(asset) ?? MissingReferenceReason.NoExportCollection;
				ReportMissingReference(pptr, reason, pointer.FileID);
			}
			return pointer.ExportYaml(container.ExportVersion);
		}
		else
		{
			AssetType assetType = container.ToExportType(typeof(TAsset));
			MetaPtr pointer = MetaPtr.CreateMissingReference(GetClassID(typeof(TAsset)), assetType);
			ReportMissingReference(pptr, ClassifyFailure(pptr), pointer.FileID);
			return pointer.ExportYaml(container.ExportVersion);
		}
	}

	private MissingReferenceReason ClassifyFailure<TAsset>(PPtr<TAsset> pptr) where TAsset : IUnityObjectBase
	{
		IReadOnlyList<AssetCollection?> dependencies = CurrentAsset.Collection.Dependencies;
		if (pptr.FileID < 0 || pptr.FileID >= dependencies.Count)
		{
			return MissingReferenceReason.MissingDependency;
		}

		AssetCollection? dependency = dependencies[pptr.FileID];
		if (dependency is null)
		{
			return MissingReferenceReason.MissingDependency;
		}

		if (!dependency.TryGetAsset(pptr.PathID, out IUnityObjectBase? asset))
		{
			return MissingReferenceReason.AssetNotFound;
		}

		return asset is NullObject ? MissingReferenceReason.NullObject : MissingReferenceReason.TypeMismatch;
	}

	private void ReportMissingReference<TAsset>(PPtr<TAsset> pptr, MissingReferenceReason reason, long placeholderFileID) where TAsset : IUnityObjectBase
	{
		MissingReferenceInfo info = CreateMissingReferenceInfo(GetClassID(typeof(TAsset)));
		info.FieldPath = GetCurrentFieldPath();
		info.TargetPathID = pptr.PathID;
		info.TargetFileID = pptr.FileID;
		info.TargetSourceFile = GetDependencyName(pptr.FileID);
		info.Reason = reason;
		info.PlaceholderFileID = placeholderFileID;
		missingReferences?.Add(info);
	}

	private void ReportMissingSceneObjectReference(IUnityObjectBase target, string fieldName, MetaPtr pointer)
	{
		if (pointer.GUID != UnityGuid.MissingReference || missingReferences is null)
		{
			return;
		}

		MissingReferenceInfo info = CreateMissingReferenceInfo(target.ClassID);
		info.FieldPath = fieldName;
		info.TargetPathID = target.PathID;
		info.TargetFileID = 0;
		info.TargetSourceFile = target.Collection.Name;
		info.Reason = projectContainer?.GetMissingReferenceReason(target) ?? MissingReferenceReason.NoExportCollection;
		info.PlaceholderFileID = pointer.FileID;
		missingReferences.Add(info);
	}

	private MissingReferenceInfo CreateMissingReferenceInfo(int targetClassID)
	{
		return new MissingReferenceInfo
		{
			ReferrerPath = DocumentPath ?? string.Empty,
			ReferrerSourceFile = CurrentAsset.Collection.Name,
			ReferrerClassID = CurrentAsset.ClassID,
			ReferrerPathID = CurrentAsset.PathID,
			ReferrerExportID = container.GetExportID(CurrentAsset),
			DocumentKind = IsMetaDocument ? "Meta" : "Asset",
			TargetClassID = targetClassID,
		};
	}

	private string GetCurrentFieldPath()
	{
		if (fieldStack.Count == 0)
		{
			return string.Empty;
		}
		return string.Join('.', fieldStack.Reverse());
	}

	private string? GetDependencyName(int fileID)
	{
		IReadOnlyList<AssetCollection?> dependencies = CurrentAsset.Collection.Dependencies;
		if (fileID < 0 || fileID >= dependencies.Count)
		{
			return null;
		}
		return dependencies[fileID]?.Name;
	}
}
