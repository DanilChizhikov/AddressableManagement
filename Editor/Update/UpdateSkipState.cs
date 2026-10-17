using System;

namespace DTech.AddressableManagement.Editor.Update
{
	[Serializable]
	internal sealed class UpdateSkipState
	{
		public bool SkipAlways;
		public string SkippedVersion;
	}
}
