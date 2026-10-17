using System;
using System.Collections.Generic;
using DTech.AddressableManagement.Internal.Diagnostics;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal sealed class ContentRoute : IContentRoute
	{
		public event Action<IContentRoute> OnChanged;

		private readonly AssetLog _log;

		public string ActiveRoot => _roots.Length == 0 ? null : _roots[_rootIndex];
		public string ActiveMirror => _mirrors.Length == 0 ? null : _mirrors[_mirrorIndex];
		public int Generation { get; private set; }
		public int MirrorCount => _mirrors.Length;
		public string ProbePath { get; private set; }
		public TimeSpan ProbeTimeout { get; private set; }

		private string[] _roots = Array.Empty<string>();
		private string[] _mirrors = Array.Empty<string>();
		private int _rootIndex;
		private int _mirrorIndex;

		public ContentRoute(ContentDeliverySettings settings, AssetLog log)
		{
			_log = log;
			Configure(settings);
		}

		public void Apply(ContentDeliverySettings settings)
		{
			Configure(settings);
			Generation++;
			OnChanged?.Invoke(this);
		}

		public void Reset()
		{
			if (_rootIndex == 0 &&
				_mirrorIndex == 0)
			{
				return;
			}

			_rootIndex = 0;
			_mirrorIndex = 0;
			Generation++;
			OnChanged?.Invoke(this);
		}

		public bool TryAdvance(FailureKind kind, int observedGeneration)
		{
			if (observedGeneration != Generation)
			{
				return true;
			}

			bool advanced = FailureClassifier.MovesRoot(kind)
				? TryStep(ref _rootIndex, _roots.Length)
				: FailureClassifier.MovesMirror(kind) && TryStep(ref _mirrorIndex, _mirrors.Length);

			if (!advanced)
			{
				return false;
			}

			Generation++;
			_log.Warning($"{kind} failure; requests now go to root '{ActiveRoot ?? "<catalog>"}' through mirror '{ActiveMirror ?? "<catalog>"}'.");
			OnChanged?.Invoke(this);
			return true;
		}

		public string Rewrite(string url)
		{
			if (string.IsNullOrEmpty(url) ||
				(_roots.Length == 0 && _mirrors.Length == 0))
			{
				return url;
			}

			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri requested) ||
				!IsHttp(requested))
			{
				return url;
			}

			string origin = requested.GetLeftPart(UriPartial.Authority);
			string pathAndQuery = requested.PathAndQuery;
			if (_roots.Length > 0)
			{
				string path = requested.AbsolutePath;
				string fileName = path.Substring(path.LastIndexOf('/') + 1);
				if (fileName.Length == 0)
				{
					return url;
				}

				var root = new Uri(ActiveRoot);
				origin = root.GetLeftPart(UriPartial.Authority);
				pathAndQuery = root.AbsolutePath.TrimEnd('/') + "/" + fileName + requested.Query;
			}

			if (_mirrors.Length > 0)
			{
				origin = ActiveMirror;
			}

			return origin + pathAndQuery;
		}

		private static bool TryStep(ref int index, int count)
		{
			if (index + 1 >= count)
			{
				return false;
			}

			index++;
			return true;
		}

		private static bool IsHttp(Uri uri)
		{
			return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
		}

		private void Configure(ContentDeliverySettings settings)
		{
			settings ??= new ContentDeliverySettings();
			_roots = Normalize(settings.ContentRoots, false);
			_mirrors = Normalize(settings.Mirrors, true);
			_rootIndex = 0;
			_mirrorIndex = 0;
			ProbePath = settings.ProbePath.Trim();
			ProbeTimeout = settings.ProbeTimeout;
		}

		private string[] Normalize(IReadOnlyList<string> entries, bool asOrigin)
		{
			var result = new List<string>(entries.Count);
			foreach (string entry in entries)
			{
				string value = RouteTemplates.Evaluate(entry);
				if (value.Length == 0)
				{
					continue;
				}

				if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
					!IsHttp(uri))
				{
					_log.Error($"Ignored CDN entry '{entry}': it is not an absolute http(s) URL.");
					continue;
				}

				result.Add(asOrigin ? uri.GetLeftPart(UriPartial.Authority) : value.TrimEnd('/'));
			}

			return result.ToArray();
		}
	}
}