using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement.Editor.Update
{
	internal static class ReleaseNotesLoader
	{
		private const string ReleasesPath = "/releases?per_page=100";
		private const string FullChangelogPrefix = "**Full Changelog**";

		public static async Task<List<ReleaseNote>> LoadAsync(Version current, Version latest, CancellationToken cancellationToken)
		{
			string json = await GitHubApi.GetAsync(ReleasesPath, cancellationToken);
			return Select(GitHubApi.ParseArray<GitHubRelease>(json), current, latest);
		}

		internal static List<ReleaseNote> Select(IReadOnlyList<GitHubRelease> releases, Version current, Version latest)
		{
			var notes = new List<ReleaseNote>();
			foreach (GitHubRelease release in releases)
			{
				if (release == null || release.draft || release.prerelease)
				{
					continue;
				}

				Version version = VersionTags.Parse(release.tag_name);
				if (version == null || (current != null && version <= current) || (latest != null && version > latest))
				{
					continue;
				}

				notes.Add(new ReleaseNote(version, release.name, CleanBody(release.body), release.html_url));
			}

			notes.Sort(NewestFirst);
			return notes;
		}

		internal static string CleanBody(string body)
		{
			if (string.IsNullOrWhiteSpace(body))
			{
				return string.Empty;
			}

			string[] lines = body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			var builder = new StringBuilder(body.Length);
			foreach (string line in lines)
			{
				if (line.TrimStart().StartsWith(FullChangelogPrefix, StringComparison.Ordinal))
				{
					continue;
				}

				builder.Append(line).Append('\n');
			}

			return builder.ToString().Trim();
		}

		private static int NewestFirst(ReleaseNote left, ReleaseNote right)
		{
			return right.Version.CompareTo(left.Version);
		}
	}
}
