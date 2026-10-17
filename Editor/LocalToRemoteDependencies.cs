using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace DTech.AddressableManagement.Editor
{
	internal static class LocalToRemoteDependencies
	{
		public static List<string> Find(AddressableAssetSettings settings)
		{
			var remoteOwners = new Dictionary<string, string>(StringComparer.Ordinal);
			var localEntries = new List<KeyValuePair<string, AddressableAssetEntry>>();
			foreach (AddressableAssetGroup group in settings.groups)
			{
				BundledAssetGroupSchema schema = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
				if (schema == null)
				{
					continue;
				}

				bool isRemote = IsRemote(schema, settings);
				var entries = new List<AddressableAssetEntry>();
				group.GatherAllAssets(entries, true, true, false);
				foreach (AddressableAssetEntry entry in entries)
				{
					if (isRemote)
					{
						remoteOwners[entry.guid] = group.Name;
					}
					else
					{
						localEntries.Add(new KeyValuePair<string, AddressableAssetEntry>(group.Name, entry));
					}
				}
			}

			var problems = new List<string>();
			foreach (KeyValuePair<string, AddressableAssetEntry> local in localEntries)
			{
				AddressableAssetEntry entry = local.Value;
				foreach (string dependency in AssetDatabase.GetDependencies(entry.AssetPath, true))
				{
					string guid = AssetDatabase.AssetPathToGUID(dependency);
					if (guid != entry.guid && remoteOwners.TryGetValue(guid, out string remoteGroup))
					{
						problems.Add($"{local.Key}{AnalyzeRule.kDelimiter}{entry.AssetPath}" +
							$"{AnalyzeRule.kDelimiter}{dependency} (remote group {remoteGroup})");
					}
				}
			}

			return problems;
		}

		private static bool IsRemote(BundledAssetGroupSchema schema, AddressableAssetSettings settings)
		{
			if (schema.LoadPath.GetName(settings) == AddressableAssetSettings.kRemoteLoadPath)
			{
				return true;
			}

			string loadPath = schema.LoadPath.GetValue(settings) ?? string.Empty;
			return loadPath.IndexOf("://", StringComparison.Ordinal) > 0 &&
				!loadPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
		}
	}
}