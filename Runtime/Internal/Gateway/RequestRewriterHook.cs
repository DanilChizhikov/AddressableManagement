using System;
using DTech.AddressableManagement.Internal.Diagnostics;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal sealed class RequestRewriterHook : IDisposable
	{
		private readonly Func<string, string> _rewrite;
		private readonly AssetLog _log;

		private bool _isInstalled;

		private RequestRewriterHook(Func<string, string> rewrite, AssetLog log)
		{
			_rewrite = rewrite;
			_log = log;
		}

		public static RequestRewriterHook Install(Func<string, string> rewrite, AssetLog log)
		{
			if (IsAnotherHookInstalled())
			{
				log.Error("Another AssetService already rewrites Addressables web requests. Build one service per application.");
			}

			var hook = new RequestRewriterHook(rewrite, log);
			Addressables.WebRequestOverride += hook.Rewrite;
			hook._isInstalled = true;
			return hook;
		}

		public void Dispose()
		{
			if (!_isInstalled)
			{
				return;
			}

			_isInstalled = false;
			Addressables.WebRequestOverride -= Rewrite;
		}

		private static bool IsAnotherHookInstalled()
		{
			Action<UnityWebRequest> current = Addressables.WebRequestOverride;
			if (current == null)
			{
				return false;
			}

			foreach (Delegate entry in current.GetInvocationList())
			{
				if (entry.Target is RequestRewriterHook)
				{
					return true;
				}
			}

			return false;
		}

		private void Rewrite(UnityWebRequest request)
		{
			string original = request.url;
			string rewritten = _rewrite(original);
			if (string.Equals(original, rewritten, StringComparison.Ordinal))
			{
				return;
			}

			request.url = rewritten;
			_log.Info($"Request '{original}' goes to '{rewritten}'.");
		}
	}
}