using System;
using DTech.AddressableManagement.Internal.Delivery;

namespace DTech.AddressableManagement.Editor
{
	internal static class ProfileRootTemplate
	{
		private const string BuildTargetToken = "[BuildTarget]";
		private const string VersionToken = "[UnityEngine.Application.version]";
		private const string RuntimeVersionToken = "{UnityEngine.Application.version}";
		private const int MaxPasses = 3;

		public static string FromLoadPath(string rawLoadPath, Func<string, string> evaluateProfileTokens)
		{
			if (string.IsNullOrWhiteSpace(rawLoadPath))
			{
				return string.Empty;
			}

			string value = KeepRuntimeTokens(rawLoadPath.Trim());
			for (int pass = 0; pass < MaxPasses && value.IndexOf('[') >= 0; pass++)
			{
				value = KeepRuntimeTokens(evaluateProfileTokens(value));
			}

			return value.TrimEnd('/');
		}

		public static bool IsHttpTemplate(string template)
		{
			return template.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
				template.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
		}

		private static string KeepRuntimeTokens(string value)
		{
			return value.Replace(BuildTargetToken, RouteTemplates.PlatformToken)
				.Replace(VersionToken, RuntimeVersionToken);
		}
	}
}