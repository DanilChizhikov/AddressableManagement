using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DTech.AddressableManagement.Editor.Update
{
	internal static class PackageUpdates
	{
		public static event Action OnChanged;

		public const string Tag = "[AddressableManagement]";

		private const string CheckedKey = "DTech.AddressableManagement.Update.Checked";
		private const string LatestKey = "DTech.AddressableManagement.Update.Latest";

		public static UpdateStatus Status { get; private set; }
		public static Version Latest { get; private set; }
		public static Version Current => _package.Current;

		private static InstalledPackage _package;
		private static IVersionFeed _feed;
		private static Task _check = Task.CompletedTask;

		public static void EnsureChecked()
		{
			if (Status != UpdateStatus.Unknown)
			{
				return;
			}

			if (!TryResolve())
			{
				return;
			}

			if (SessionState.GetBool(CheckedKey, false))
			{
				Apply(VersionTags.Parse(SessionState.GetString(LatestKey, string.Empty)));
				return;
			}

			_check = CheckAsync();
		}

		public static Task RecheckAsync()
		{
			if (Status == UpdateStatus.Unknown && !TryResolve())
			{
				return Task.CompletedTask;
			}

			if (Status == UpdateStatus.Unsupported || Status == UpdateStatus.Installing)
			{
				return Task.CompletedTask;
			}

			if (Status != UpdateStatus.Checking)
			{
				_check = CheckAsync();
			}

			return _check;
		}

		public static async Task InstallAsync()
		{
			if (Status != UpdateStatus.Available)
			{
				return;
			}

			Version target = Latest;
			SetStatus(UpdateStatus.Installing);
			try
			{
				AddRequest request = Client.Add(_package.InstallIdentifier(target));
				await EditorRequests.WaitAsync(request, CancellationToken.None);
				if (request.Status != StatusCode.Success)
				{
					throw new InvalidOperationException(request.Error?.message ?? "Package update failed.");
				}

				AssetDatabase.Refresh();
				SetStatus(UpdateStatus.UpToDate);
			}
			catch (Exception exception)
			{
				Debug.LogError($"{Tag} Update to v{target} failed: {exception.Message}");
				SetStatus(UpdateStatus.Available);
			}
		}

		private static bool TryResolve()
		{
			_feed = InstalledPackage.TryResolve(out _package) ? CreateFeed(_package) : null;
			if (_feed != null)
			{
				return true;
			}

			SetStatus(UpdateStatus.Unsupported);
			return false;
		}

		private static IVersionFeed CreateFeed(InstalledPackage package)
		{
			switch (package.Source)
			{
				case PackageSource.Git:
					return string.IsNullOrEmpty(package.GitUrl) ? null : new GitTagFeed();
				case PackageSource.Registry:
					return new RegistryFeed(package.Name);
				default:
					return null;
			}
		}

		private static async Task CheckAsync()
		{
			SetStatus(UpdateStatus.Checking);
			try
			{
				Version latest = await _feed.FetchLatestAsync(CancellationToken.None);
				SessionState.SetBool(CheckedKey, true);
				SessionState.SetString(LatestKey, latest?.ToString() ?? string.Empty);
				Apply(latest);
			}
			catch (Exception exception)
			{
				SessionState.SetBool(CheckedKey, true);
				SessionState.SetString(LatestKey, string.Empty);
				Debug.LogWarning($"{Tag} Update check failed: {exception.Message}");
				SetStatus(UpdateStatus.Failed);
			}
		}

		private static void Apply(Version latest)
		{
			Latest = latest;
			SetStatus(latest != null && latest > _package.Current ? UpdateStatus.Available : UpdateStatus.UpToDate);
		}

		private static void SetStatus(UpdateStatus status)
		{
			Status = status;
			OnChanged?.Invoke();
		}
	}
}
