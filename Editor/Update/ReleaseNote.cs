using System;

namespace DTech.AddressableManagement.Editor.Update
{
	internal readonly struct ReleaseNote
	{
		public Version Version { get; }
		public string Title { get; }
		public string Body { get; }
		public string Url { get; }

		public ReleaseNote(Version version, string title, string body, string url)
		{
			Version = version;
			Title = title;
			Body = body;
			Url = url;
		}
	}
}
