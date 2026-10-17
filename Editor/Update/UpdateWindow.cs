using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.AddressableManagement.Editor.Update
{
	internal sealed class UpdateWindow : EditorWindow
	{
		private const string WindowTitle = "Addressable Management Update";
		private const string ShownKey = "DTech.AddressableManagement.Update.Shown";
		private const string EmptyBody = "_No description._";
		private const float WindowWidth = 700f;
		private const float WindowHeight = 600f;

		private Label _currentLabel;
		private Label _latestLabel;
		private ScrollView _notes;
		private Button _updateButton;
		private CancellationTokenSource _loading;
		private Version _notesVersion;

		private static bool WasShownThisSession
		{
			get => SessionState.GetBool(ShownKey, false);
			set => SessionState.SetBool(ShownKey, value);
		}

		[MenuItem("Tools/DTech/Addressable Management/Check Update")]
		private static void CheckFromMenu()
		{
			_ = CheckFromMenuAsync();
		}

		[InitializeOnLoadMethod]
		private static void CheckOnLoad()
		{
			if (Application.isBatchMode)
			{
				return;
			}

			EditorApplication.delayCall += StartAutoCheck;
		}

		private static async Task CheckFromMenuAsync()
		{
			await PackageUpdates.RecheckAsync();
			switch (PackageUpdates.Status)
			{
				case UpdateStatus.Available:
					Open();
					break;
				case UpdateStatus.UpToDate:
					Debug.Log($"{PackageUpdates.Tag} Already up to date: v{PackageUpdates.Current}.");
					break;
				case UpdateStatus.Unsupported:
					Debug.Log($"{PackageUpdates.Tag} Update check works only for a package installed through Package Manager from git or a registry.");
					break;
			}
		}

		private static void StartAutoCheck()
		{
			PackageUpdates.OnChanged -= AutoCheckChangedHandler;
			PackageUpdates.OnChanged += AutoCheckChangedHandler;
			PackageUpdates.EnsureChecked();
			AutoCheckChangedHandler();
		}

		private static void Open()
		{
			var window = GetWindow<UpdateWindow>(WindowTitle);
			window.minSize = new Vector2(WindowWidth, WindowHeight);
			window.maxSize = new Vector2(WindowWidth, WindowHeight);
			window.Show();
			WasShownThisSession = true;
		}

		private static string Describe(Version version)
		{
			return version == null ? "—" : $"v{version}";
		}

		private static void CreateHeader(VisualElement root)
		{
			Label title = CreateText("Addressable Management", 36, FontStyle.Bold);
			title.style.alignSelf = Align.Center;
			root.Add(title);
		}

		private static Label CreateText(string text, int fontSize, FontStyle fontStyle)
		{
			return new Label(text)
			{
				style =
				{
					fontSize = fontSize,
					unityFontStyleAndWeight = fontStyle,
					color = NoteStyles.Text,
					whiteSpace = WhiteSpace.Normal,
					flexShrink = 0,
				}
			};
		}

		private static Button CreateButton(string text, Action clicked, Color background, float height)
		{
			return new Button(clicked)
			{
				text = text,
				style =
				{
					backgroundColor = background,
					color = Color.white,
					flexGrow = 1,
					height = height,
				}
			};
		}

		private static void AutoCheckChangedHandler()
		{
			UpdateStatus status = PackageUpdates.Status;
			if (status == UpdateStatus.Unknown || status == UpdateStatus.Checking)
			{
				return;
			}

			PackageUpdates.OnChanged -= AutoCheckChangedHandler;
			if (status == UpdateStatus.Available && !WasShownThisSession && !UpdateSkips.IsSuppressed(PackageUpdates.Latest))
			{
				Open();
			}
		}

		private void OnEnable()
		{
			PackageUpdates.OnChanged -= ChangedHandler;
			PackageUpdates.OnChanged += ChangedHandler;
		}

		private void OnDisable()
		{
			PackageUpdates.OnChanged -= ChangedHandler;
			CancelLoading();
		}

		private void CreateGUI()
		{
			VisualElement root = rootVisualElement;
			root.Clear();
			root.style.paddingLeft = 20;
			root.style.paddingRight = 20;
			root.style.paddingTop = 20;
			root.style.paddingBottom = 20;

			CreateHeader(root);
			CreateDescription(root);
			CreateChangelog(root);
			CreateFooter(root);

			PackageUpdates.EnsureChecked();
			Refresh();
		}

		private void CreateDescription(VisualElement root)
		{
			var description = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					paddingTop = 28,
					paddingBottom = 20,
					flexShrink = 0,
				}
			};

			Label headline = CreateText("New version available to update!", 18, FontStyle.Normal);
			headline.style.alignSelf = Align.Center;
			_currentLabel = CreateText(string.Empty, 13, FontStyle.Normal);
			_currentLabel.style.paddingTop = 6;
			_latestLabel = CreateText(string.Empty, 13, FontStyle.Normal);

			description.Add(headline);
			description.Add(_currentLabel);
			description.Add(_latestLabel);
			root.Add(description);
		}

		private void CreateChangelog(VisualElement root)
		{
			Label title = CreateText("What's New", 15, FontStyle.Bold);
			title.style.marginBottom = 6;

			_notes = new ScrollView(ScrollViewMode.Vertical)
			{
				style =
				{
					flexGrow = 1,
					minHeight = 0,
				},
				horizontalScrollerVisibility = ScrollerVisibility.Hidden,
			};

			NoteStyles.ApplyFrame(_notes);
			root.Add(title);
			root.Add(_notes);
		}

		private void CreateFooter(VisualElement root)
		{
			var footer = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Column,
					paddingTop = 22,
					flexShrink = 0,
				}
			};

			var skipRow = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					flexShrink = 0,
				}
			};

			skipRow.Add(CreateButton("Skip Version", SkipVersionClickHandler, new Color(1f, 0.5f, 0.3f), 32));
			skipRow.Add(CreateButton("Skip Always", SkipAlwaysClickHandler, new Color(1f, 0.3f, 0.3f), 32));
			_updateButton = CreateButton("Update", UpdateClickHandler, new Color(0.4f, 0.6f, 0.3f), 44);
			_updateButton.style.flexGrow = 0;

			footer.Add(skipRow);
			footer.Add(_updateButton);
			root.Add(footer);
		}

		private void Refresh()
		{
			if (_notes == null)
			{
				return;
			}

			UpdateStatus status = PackageUpdates.Status;
			if (status != UpdateStatus.Checking && status != UpdateStatus.Available && status != UpdateStatus.Installing)
			{
				EditorApplication.delayCall += CloseIfOpen;
				return;
			}

			_currentLabel.text = $"Current version: {Describe(PackageUpdates.Current)}";
			_latestLabel.text = $"New version: {Describe(PackageUpdates.Latest)}";
			_updateButton.text = status == UpdateStatus.Installing ? "Updating…" : "Update";
			_updateButton.SetEnabled(status == UpdateStatus.Available);
			if (status == UpdateStatus.Available && _notesVersion != PackageUpdates.Latest)
			{
				_ = LoadNotesAsync();
			}
		}

		private async Task LoadNotesAsync()
		{
			CancelLoading();
			_notesVersion = PackageUpdates.Latest;
			_loading = new CancellationTokenSource();
			CancellationToken cancellationToken = _loading.Token;
			_notes.Clear();
			_notes.Add(NoteStyles.PlainLabel("Loading release notes…", NoteStyles.Muted));
			try
			{
				List<ReleaseNote> notes = await ReleaseNotesLoader.LoadAsync(PackageUpdates.Current, PackageUpdates.Latest, cancellationToken);
				if (!cancellationToken.IsCancellationRequested)
				{
					ShowNotes(notes);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception exception)
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					ShowFailure($"Failed to load release notes: {exception.Message}");
				}
			}
		}

		private void ShowNotes(List<ReleaseNote> notes)
		{
			if (notes.Count == 0)
			{
				ShowFailure("No release notes are published for these versions.");
				return;
			}

			_notes.Clear();
			foreach (ReleaseNote note in notes)
			{
				string title = $"v{note.Version}";
				if (!string.IsNullOrWhiteSpace(note.Title) && note.Title.Trim() != title)
				{
					title += $"  —  {note.Title.Trim()}";
				}

				Label header = CreateText(title, 15, FontStyle.Bold);
				header.style.marginTop = 10;
				header.style.marginLeft = 10;
				header.style.marginRight = 10;
				_notes.Add(header);

				var body = new ReleaseNotesView();
				body.SetText(string.IsNullOrEmpty(note.Body) ? EmptyBody : note.Body);
				_notes.Add(body);
			}
		}

		private void ShowFailure(string message)
		{
			_notes.Clear();
			Label label = NoteStyles.PlainLabel(message, NoteStyles.Muted);
			label.style.whiteSpace = WhiteSpace.Normal;
			label.style.marginTop = 8;
			label.style.marginLeft = 10;
			label.style.marginRight = 10;
			_notes.Add(label);
			_notes.Add(new Button(OpenReleasesPage) { text = "Open releases page" });
		}

		private void CancelLoading()
		{
			if (_loading == null)
			{
				return;
			}

			_loading.Cancel();
			_loading.Dispose();
			_loading = null;
		}

		private void OpenReleasesPage()
		{
			Application.OpenURL(GitHubApi.ReleasesPage);
		}

		private void CloseIfOpen()
		{
			if (this != null)
			{
				Close();
			}
		}

		private void ChangedHandler()
		{
			Refresh();
		}

		private void SkipVersionClickHandler()
		{
			UpdateSkips.SkipVersion(PackageUpdates.Latest);
			Close();
		}

		private void SkipAlwaysClickHandler()
		{
			UpdateSkips.SkipAlways();
			Close();
		}

		private void UpdateClickHandler()
		{
			_ = PackageUpdates.InstallAsync();
		}
	}
}
