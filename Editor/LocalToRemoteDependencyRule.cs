using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;

namespace DTech.AddressableManagement.Editor
{
	internal sealed class LocalToRemoteDependencyRule : AnalyzeRule
	{
		public override string ruleName => "Check Local Assets Depending On Remote Assets (DTech)";

		public override List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)
		{
			var results = new List<AnalyzeResult>();
			if (settings == null)
			{
				return results;
			}

			List<string> problems = LocalToRemoteDependencies.Find(settings);
			if (problems.Count == 0)
			{
				results.Add(noErrors);
				return results;
			}

			foreach (string problem in problems)
			{
				results.Add(new AnalyzeResult
				{
					resultName = problem,
					severity = MessageType.Error,
				});
			}

			return results;
		}
	}
}