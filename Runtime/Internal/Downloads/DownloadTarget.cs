using System;
using System.Collections.Generic;

namespace DTech.AddressableManagement.Internal.Downloads
{
	internal readonly struct DownloadTarget
	{
		public static DownloadTarget RemoteContent => new(Array.Empty<string>(), true, "remote content");

		public IReadOnlyList<string> Keys { get; }
		public bool IsRemoteContent { get; }
		public string Description { get; }

		private DownloadTarget(IReadOnlyList<string> keys, bool isRemoteContent, string description)
		{
			Keys = keys;
			IsRemoteContent = isRemoteContent;
			Description = description;
		}

		public static DownloadTarget ForKeys(IReadOnlyList<string> keys)
		{
			string description = keys.Count == 1 ? keys[0] : $"{keys.Count} keys";
			return new DownloadTarget(keys, false, description);
		}
	}
}