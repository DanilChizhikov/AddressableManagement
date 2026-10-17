using System;
using System.Collections.Generic;
using UnityEngine;

namespace DTech.AddressableManagement.Internal.Diagnostics
{
	internal sealed class AssetLog
	{
		private const string Tag = "[AddressableManagement]";

		private readonly HashSet<string> _reportedErrors = new(StringComparer.Ordinal);

		public bool Enabled { get; set; }

		public void Info(string message)
		{
			if (Enabled)
			{
				Debug.Log($"{Tag} {message}");
			}
		}

		public void Warning(string message)
		{
			Debug.LogWarning($"{Tag} {message}");
		}

		public void Error(string message)
		{
			Debug.LogError($"{Tag} {message}");
		}

		public void ErrorOnce(string key, string message)
		{
			if (_reportedErrors.Add(key))
			{
				Error(message);
			}
		}
	}
}