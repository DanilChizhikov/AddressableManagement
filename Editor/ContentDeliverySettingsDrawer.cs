using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DTech.AddressableManagement.Editor
{
	[CustomPropertyDrawer(typeof(ContentDeliverySettings))]
	internal sealed class ContentDeliverySettingsDrawer : PropertyDrawer
	{
		private const string RootsField = "_contentRoots";
		private const string AddRootLabel = "Add Root From Addressables Profile";

		private static readonly string[] _fields =
		{
			RootsField,
			"_mirrors",
			"_probePath",
			"_probeTimeoutSeconds"
		};

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;
			if (!property.isExpanded)
			{
				return height;
			}

			foreach (string field in _fields)
			{
				height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) +
					EditorGUIUtility.standardVerticalSpacing;
			}

			return height + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			var line = new Rect(position.x, position.y, position.width,
				EditorGUIUtility.singleLineHeight);

			property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label,
				true);

			if (property.isExpanded)
			{
				EditorGUI.indentLevel++;
				float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;
				foreach (string field in _fields)
				{
					SerializedProperty child = property.FindPropertyRelative(field);
					float height = EditorGUI.GetPropertyHeight(child, true);
					EditorGUI.PropertyField(new Rect(position.x, y, position.width,
						height), child, true);

					y += height + EditorGUIUtility.standardVerticalSpacing;
				}

				Rect button = EditorGUI.IndentedRect(new Rect(position.x, y, position.width,
					EditorGUIUtility.singleLineHeight));

				if (GUI.Button(button, AddRootLabel))
				{
					ShowProfiles(button, property.serializedObject, property.propertyPath);
				}

				EditorGUI.indentLevel--;
			}

			EditorGUI.EndProperty();
		}

		private static void ShowProfiles(Rect button, SerializedObject serializedObject, string propertyPath)
		{
			var menu = new GenericMenu();
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			if (settings == null)
			{
				menu.AddDisabledItem(new GUIContent("No Addressables settings in the project"));
				menu.DropDown(button);
				return;
			}

			AddressableAssetProfileSettings profiles = settings.profileSettings;
			foreach (string profileName in profiles.GetAllProfileNames())
			{
				string profileId = profiles.GetProfileId(profileName);
				string rawLoadPath = profiles.GetValueByName(profileId, AddressableAssetSettings.kRemoteLoadPath);
				string template = ProfileRootTemplate.FromLoadPath(rawLoadPath,
					value => profiles.EvaluateString(profileId, value));

				var item = new GUIContent($"{profileName}    {template.Replace('/', '∕')}");
				if (!ProfileRootTemplate.IsHttpTemplate(template))
				{
					menu.AddDisabledItem(item);
					continue;
				}

				menu.AddItem(item, false, () => AppendRoot(serializedObject, propertyPath, template));
			}

			menu.DropDown(button);
		}

		private static void AppendRoot(SerializedObject serializedObject, string propertyPath, string template)
		{
			serializedObject.Update();
			SerializedProperty roots = serializedObject.FindProperty(propertyPath)?.FindPropertyRelative(RootsField);
			if (roots == null)
			{
				return;
			}

			int index = roots.arraySize;
			roots.InsertArrayElementAtIndex(index);
			roots.GetArrayElementAtIndex(index).stringValue = template;
			serializedObject.ApplyModifiedProperties();
		}
	}
}