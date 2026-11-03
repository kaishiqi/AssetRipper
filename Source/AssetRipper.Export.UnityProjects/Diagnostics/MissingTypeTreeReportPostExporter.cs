using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Writes a report describing serialized files without type trees and MonoBehaviours whose
/// fields could not be parsed.
/// </summary>
public sealed class MissingTypeTreeReportPostExporter : IPostExporter
{
	public const string FileName = "missing-typetree.json";

	private const int MaxSamplesPerCollection = 100;
	private const int MaxParseFailureSamples = 100;

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		MissingTypeTreeReport report = new() { ExportVersion = settings.Version.ToString() };
		Dictionary<string, int> scriptCounts = new(StringComparer.Ordinal);

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			if (collection is not SerializedAssetCollection serialized)
			{
				continue;
			}

			List<IMonoBehaviour> monoBehaviours = collection.OfType<IMonoBehaviour>().ToList();
			if (monoBehaviours.Count == 0)
			{
				continue;
			}

			if (!serialized.HasTypeTree)
			{
				report.TypelessCollectionCount++;
				TypelessCollectionEntry entry = new()
				{
					Collection = collection.Name,
					MonoBehaviourCount = monoBehaviours.Count,
				};

				foreach (IMonoBehaviour monoBehaviour in monoBehaviours)
				{
					report.TypelessMonoBehaviourCount++;
					string script = MonoBehaviourScriptHelper.GetScriptName(monoBehaviour) ?? "(unknown)";
					scriptCounts[script] = scriptCounts.TryGetValue(script, out int count) ? count + 1 : 1;

					if (monoBehaviour.Structure is null)
					{
						entry.FailedCount++;
					}

					if (entry.Samples.Count < MaxSamplesPerCollection)
					{
						entry.Samples.Add(CreateSample(monoBehaviour));
					}
				}

				report.Collections.Add(entry);
			}
			else
			{
				foreach (IMonoBehaviour monoBehaviour in monoBehaviours)
				{
					if (monoBehaviour.Structure is null)
					{
						report.ParseFailureCount++;
						if (report.ParseFailures.Count < MaxParseFailureSamples)
						{
							report.ParseFailures.Add(CreateSample(monoBehaviour));
						}
					}
				}
			}
		}

		report.Collections.Sort((a, b) => string.CompareOrdinal(a.Collection, b.Collection));
		report.Scripts = scriptCounts
			.OrderByDescending(pair => pair.Value)
			.ThenBy(pair => pair.Key, StringComparer.Ordinal)
			.Select(pair => new ScriptSummary { Script = pair.Key, Count = pair.Value })
			.ToList();

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static MonoBehaviourSample CreateSample(IMonoBehaviour monoBehaviour)
	{
		(string? script, string? assembly) = MonoBehaviourScriptHelper.GetScript(monoBehaviour);
		return new MonoBehaviourSample
		{
			Name = monoBehaviour.GetBestName(),
			PathID = monoBehaviour.PathID,
			Script = script,
			Assembly = assembly,
			Structure = monoBehaviour.Structure switch
			{
				null => "Failed",
				UnloadedStructure => "Unloaded",
				SerializableStructure => "Loaded",
				_ => monoBehaviour.Structure.GetType().Name,
			},
			FieldCount = monoBehaviour.Structure is SerializableStructure structure ? structure.Type.Fields.Count : 0,
		};
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, MissingTypeTreeReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, MissingTypeTreeReportSerializerContext.Default.MissingTypeTreeReport);
		stream.Flush();
	}

	private static void Log(MissingTypeTreeReport report)
	{
		if (report.TypelessMonoBehaviourCount == 0)
		{
			Logger.Info(LogCategory.Export, "All serialized files with MonoBehaviours have type trees.");
		}
		else
		{
			Logger.Warning(LogCategory.Export,
				$"{report.TypelessCollectionCount} serialized files have no type tree ({report.TypelessMonoBehaviourCount} MonoBehaviours). See '{FileName}'.");
		}

		if (report.ParseFailureCount > 0)
		{
			Logger.Warning(LogCategory.Export,
				$"{report.ParseFailureCount} MonoBehaviours with type trees failed to parse. See '{FileName}'.");
		}
	}
}
