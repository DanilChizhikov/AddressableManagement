using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement.Editor.Update
{
	internal sealed class GitTagFeed : IVersionFeed
	{
		private const string TagsPath = "/tags?per_page=100";

		public async Task<Version> FetchLatestAsync(CancellationToken cancellationToken)
		{
			string json = await GitHubApi.GetAsync(TagsPath, cancellationToken);
			Version latest = null;
			foreach (GitHubTag tag in GitHubApi.ParseArray<GitHubTag>(json))
			{
				latest = VersionTags.Max(latest, VersionTags.Parse(tag.name));
			}

			return latest;
		}
	}
}
