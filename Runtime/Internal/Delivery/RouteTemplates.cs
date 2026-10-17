using System;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal static class RouteTemplates
	{
		private const string PlatformToken = "{Platform}";

		public static string Evaluate(string template)
		{
			if (string.IsNullOrWhiteSpace(template))
			{
				return string.Empty;
			}

			string value = template.Trim();
			if (value.IndexOf(PlatformToken, StringComparison.Ordinal) >= 0)
			{
				value = value.Replace(PlatformToken, PlatformMappingService.GetPlatformPathSubFolder());
			}

			return value.IndexOf('{') >= 0 ? AddressablesRuntimeProperties.EvaluateString(value) : value;
		}
	}
}