using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.Processing;
using AssetRipper.SerializationLogic;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using System.Text.Json;

namespace AssetRipper.Export.UnityProjects.Diagnostics;

/// <summary>
/// Scores typeless MonoBehaviours by their default value ratio and compares each one with the
/// same script in files that have type trees.
/// </summary>
public sealed class TypeTreeConfidenceReportPostExporter : IPostExporter
{
	public const string FileName = "typetree-confidence.json";

	private const int MaxBaselineSamplesPerScript = 50;
	private const int MaxStructureDepth = 4;
	private const int MinFieldsForSuspicion = 3;
	private const double SuspicionThreshold = 0.25;
	private const double HighDefaultRatio = 0.95;

	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		TypeTreeConfidenceReport report = new() { ExportVersion = settings.Version.ToString() };
		List<TypelessRecord> typeless = new();
		Dictionary<string, List<double>> baselineByScript = new(StringComparer.Ordinal);
		List<double> globalBaseline = new();

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			if (collection is not SerializedAssetCollection serialized)
			{
				continue;
			}

			foreach (IMonoBehaviour monoBehaviour in collection.OfType<IMonoBehaviour>())
			{
				if (monoBehaviour.Structure is not SerializableStructure structure)
				{
					continue;
				}

				(int total, int defaults) = Analyze(structure, 0);
				if (total == 0)
				{
					continue;
				}

				double ratio = (double)defaults / total;
				string script = MonoBehaviourScriptHelper.GetScriptName(monoBehaviour) ?? "(unknown)";

				if (serialized.HasTypeTree)
				{
					if (!baselineByScript.TryGetValue(script, out List<double>? samples))
					{
						samples = new List<double>();
						baselineByScript.Add(script, samples);
					}
					if (samples.Count < MaxBaselineSamplesPerScript)
					{
						samples.Add(ratio);
					}
					globalBaseline.Add(ratio);
				}
				else
				{
					typeless.Add(new TypelessRecord(monoBehaviour, collection.Name, script, total, defaults, ratio));
				}
			}
		}

		report.TypelessMonoBehaviourCount = typeless.Count;
		report.GlobalBaselineRatio = Round(globalBaseline.Count > 0 ? globalBaseline.Average() : 0);

		Dictionary<string, ScriptBaseline> baselines = new(StringComparer.Ordinal);
		foreach ((string script, List<double> samples) in baselineByScript)
		{
			baselines.Add(script, new ScriptBaseline
			{
				Script = script,
				Samples = samples.Count,
				DefaultRatio = Round(samples.Count > 0 ? samples.Average() : 0),
			});
		}

		foreach (TypelessRecord record in typeless)
		{
			double baseline = report.GlobalBaselineRatio;
			int baselineSamples = 0;
			if (baselines.TryGetValue(record.Script, out ScriptBaseline? scriptBaseline) && scriptBaseline.Samples >= 3)
			{
				baseline = scriptBaseline.DefaultRatio;
				baselineSamples = scriptBaseline.Samples;
			}

			double suspicion = Math.Max(0, record.Ratio - baseline);
			report.EvaluatedCount++;
			report.Entries.Add(new ConfidenceEntry
			{
				Name = record.MonoBehaviour.GetBestName(),
				PathID = record.MonoBehaviour.PathID,
				Collection = record.Collection,
				Script = record.Script,
				FieldCount = record.Total,
				DefaultFieldCount = record.Defaults,
				DefaultRatio = Round(record.Ratio),
				BaselineRatio = baseline,
				BaselineSamples = baselineSamples,
				Suspicion = Round(suspicion),
				Suspicious = record.Total >= MinFieldsForSuspicion
					&& record.Ratio >= HighDefaultRatio
					&& suspicion >= SuspicionThreshold,
			});
			if (report.Entries[^1].Suspicious)
			{
				report.SuspiciousCount++;
			}
		}

		report.Entries.Sort((a, b) =>
		{
			int result = b.Suspicion.CompareTo(a.Suspicion);
			if (result != 0)
			{
				return result;
			}
			result = b.DefaultRatio.CompareTo(a.DefaultRatio);
			if (result != 0)
			{
				return result;
			}
			result = string.CompareOrdinal(a.Collection, b.Collection);
			return result != 0 ? result : a.PathID.CompareTo(b.PathID);
		});

		report.Scripts = baselines.Values
			.OrderByDescending(b => b.Samples)
			.ThenBy(b => b.Script, StringComparer.Ordinal)
			.ToList();

		Write(settings, fileSystem, report);
		Log(report);
	}

	private static (int Total, int Defaults) Analyze(SerializableStructure structure, int depth)
	{
		int total = 0;
		int defaults = 0;
		int count = Math.Min(structure.Fields.Length, structure.Type.Fields.Count);
		for (int i = 0; i < count; i++)
		{
			total++;
			if (IsDefault(structure.Fields[i], structure.Type.Fields[i], depth))
			{
				defaults++;
			}
		}
		return (total, defaults);
	}

	private static bool IsDefault(SerializableValue value, SerializableType.Field field, int depth)
	{
		if (field.ArrayDepth > 0)
		{
			return value.CValue is not Array array || array.Length == 0;
		}
		return IsDefault(value, field.Type, depth);
	}

	private static bool IsDefault(SerializableValue value, SerializableType type, int depth)
	{
		switch (type.Type)
		{
			case PrimitiveType.String:
				return string.IsNullOrEmpty(value.CValue as string);
			case PrimitiveType.Pair:
			case PrimitiveType.MapPair:
				if (value.CValue is SerializablePair pair)
				{
					return IsDefault(pair.First, pair.Type.Fields[0], depth)
						&& IsDefault(pair.Second, pair.Type.Fields[1], depth);
				}
				return value.CValue is null;
			case PrimitiveType.Complex:
				if (type.IsEnginePointer())
				{
					return value.CValue is not IPPtr pointer || pointer.PathID == 0;
				}
				if (depth >= MaxStructureDepth)
				{
					return false;
				}
				if (value.CValue is SerializableStructure structure)
				{
					(int total, int defaults) = Analyze(structure, depth + 1);
					return total == 0 || defaults == total;
				}
				return value.CValue is null;
			default:
				return value.PValue == 0;
		}
	}

	private static void Write(FullConfiguration settings, FileSystem fileSystem, TypeTreeConfidenceReport report)
	{
		string outputDirectory = settings.AuxiliaryFilesPath;
		fileSystem.Directory.Create(outputDirectory);
		using Stream stream = fileSystem.File.Create(fileSystem.Path.Join(outputDirectory, FileName));
		JsonSerializer.Serialize(stream, report, TypeTreeConfidenceReportSerializerContext.Default.TypeTreeConfidenceReport);
		stream.Flush();
	}

	private static void Log(TypeTreeConfidenceReport report)
	{
		if (report.EvaluatedCount == 0)
		{
			Logger.Info(LogCategory.Export, "No typeless MonoBehaviours were available for confidence scoring.");
			return;
		}

		Logger.Info(LogCategory.Export,
			$"Scored {report.EvaluatedCount} typeless MonoBehaviours; {report.SuspiciousCount} look suspicious. See '{FileName}'.");

		if (report.SuspiciousCount > 0)
		{
			Logger.Warning(LogCategory.Export,
				$"{report.SuspiciousCount} typeless MonoBehaviours have almost only default field values compared with their script baseline.");
		}
	}

	private static double Round(double value)
	{
		return Math.Round(value, 4);
	}

	private readonly record struct TypelessRecord(
		IMonoBehaviour MonoBehaviour,
		string Collection,
		string Script,
		int Total,
		int Defaults,
		double Ratio);
}
