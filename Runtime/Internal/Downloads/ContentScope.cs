using System.Collections.Generic;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DTech.AddressableManagement.Internal.Downloads
{
	internal readonly struct ContentScope
	{
		public static ContentScope Empty => default;

		public IReadOnlyList<string> Keys { get; }
		public IList<IResourceLocation> Locations { get; }
		public bool IsEmpty => (Keys == null || Keys.Count == 0) && (Locations == null || Locations.Count == 0);

		private ContentScope(IReadOnlyList<string> keys, IList<IResourceLocation> locations)
		{
			Keys = keys;
			Locations = locations;
		}

		public static ContentScope ForKeys(IReadOnlyList<string> keys)
		{
			return new ContentScope(keys, null);
		}

		public static ContentScope ForLocations(IList<IResourceLocation> locations)
		{
			return new ContentScope(null, locations);
		}
	}
}