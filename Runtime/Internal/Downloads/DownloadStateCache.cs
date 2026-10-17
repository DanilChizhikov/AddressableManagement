using System;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DTech.AddressableManagement.Internal.Downloads
{
	internal sealed class DownloadStateCache
	{
		private readonly HashSet<string> _cachedKeys = new HashSet<string>(StringComparer.Ordinal);

		public bool IsRemoteContentCached { get; private set; }

		private IList<IResourceLocation> _remoteContent;

		public bool IsCached(string key)
		{
			return _cachedKeys.Contains(key);
		}

		public void MarkCached(ContentScope scope)
		{
			if (scope.Locations != null)
			{
				IsRemoteContentCached = true;
				return;
			}

			if (scope.Keys == null)
			{
				return;
			}

			foreach (string key in scope.Keys)
			{
				_cachedKeys.Add(key);
			}
		}

		public IList<IResourceLocation> GetRemoteContent(Func<IList<IResourceLocation>> collect)
		{
			return _remoteContent ??= collect();
		}

		public void Clear()
		{
			_cachedKeys.Clear();
			_remoteContent = null;
			IsRemoteContentCached = false;
		}
	}
}