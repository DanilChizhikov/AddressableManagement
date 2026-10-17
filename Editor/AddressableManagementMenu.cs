using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DTech.AddressableManagement.Editor
{
	[InitializeOnLoad]
	internal static class AddressableManagementMenu
	{
		private const string Tag = "[AddressableManagement]";

		static AddressableManagementMenu()
		{
			AnalyzeSystem.RegisterNewRule<LocalToRemoteDependencyRule>();
		}

		[MenuItem("Tools/DTech/Addressable Management/Check Local To Remote Dependencies")]
		private static void CheckLocalToRemoteDependencies()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				Debug.LogWarning($"{Tag} The project has no Addressables settings.");
				return;
			}

			List<string> problems = LocalToRemoteDependencies.Find(settings);
			if (problems.Count == 0)
			{
				Debug.Log($"{Tag} No local asset depends on a remote one.");
				return;
			}

			foreach (string problem in problems)
			{
				Debug.LogError($"{Tag} Local asset depends on remote content: {problem.Replace(AnalyzeRule.kDelimiter, ' ')}");
			}
		}
	}
}