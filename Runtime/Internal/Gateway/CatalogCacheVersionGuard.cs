using System;
using System.IO;
using UnityEngine;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal static class CatalogCacheVersionGuard
	{
		private const string VersionKey = "DTech.AddressableManagement.CatalogCacheVersion";
		private const string CacheFolderName = "com.unity.addressables";

		public static bool ResetIfVersionChanged()
		{
			string current = Application.version;
			string stored = PlayerPrefs.GetString(VersionKey, string.Empty);
			if (string.Equals(stored, current, StringComparison.Ordinal))
			{
				return false;
			}

			string folder = Path.Combine(Application.persistentDataPath, CacheFolderName);
			if (Directory.Exists(folder))
			{
				Directory.Delete(folder, true);
			}

			PlayerPrefs.SetString(VersionKey, current);
			PlayerPrefs.Save();
			return true;
		}
	}
}