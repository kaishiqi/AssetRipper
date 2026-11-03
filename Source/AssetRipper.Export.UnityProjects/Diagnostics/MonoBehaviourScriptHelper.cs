using AssetRipper.Assets;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_115;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

internal static class MonoBehaviourScriptHelper
{
	public static string? GetScriptName(IMonoBehaviour monoBehaviour)
	{
		return GetScript(monoBehaviour).Name;
	}

	public static (string? Name, string? Assembly) GetScript(IMonoBehaviour monoBehaviour)
	{
		var scriptPointer = monoBehaviour.Script;
		if (!monoBehaviour.Collection.TryGetAsset(scriptPointer.FileID, scriptPointer.PathID, out IUnityObjectBase? asset)
			|| asset is not IMonoScript script)
		{
			return (null, null);
		}

		string name = script.Namespace.String;
		string className = script.ClassName_R.String;
		if (name.Length == 0)
		{
			name = className;
		}
		else
		{
			name = $"{name}.{className}";
		}

		return (name, script.GetAssemblyNameFixed());
	}
}
