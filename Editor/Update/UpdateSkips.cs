using System;
using UnityEditor;
using UnityEngine;

namespace DTech.AddressableManagement.Editor.Update
{
	internal static class UpdateSkips
	{
		private const string PrefsKey = "DTech.AddressableManagement.Update.Skips";

		public static bool IsSuppressed(Version latest)
		{
			return IsSuppressed(Load(), latest);
		}

		public static void SkipVersion(Version latest)
		{
			UpdateSkipState state = Load();
			state.SkippedVersion = latest?.ToString() ?? string.Empty;
			Save(state);
		}

		public static void SkipAlways()
		{
			UpdateSkipState state = Load();
			state.SkipAlways = true;
			Save(state);
		}

		internal static bool IsSuppressed(UpdateSkipState state, Version latest)
		{
			if (state.SkipAlways)
			{
				return true;
			}

			Version skipped = VersionTags.Parse(state.SkippedVersion);
			return skipped != null && latest != null && latest <= skipped;
		}

		private static UpdateSkipState Load()
		{
			string json = EditorPrefs.GetString(PrefsKey, string.Empty);
			if (string.IsNullOrEmpty(json))
			{
				return new UpdateSkipState();
			}

			try
			{
				return JsonUtility.FromJson<UpdateSkipState>(json) ?? new UpdateSkipState();
			}
			catch (ArgumentException)
			{
				return new UpdateSkipState();
			}
		}

		private static void Save(UpdateSkipState state)
		{
			EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(state));
		}
	}
}
