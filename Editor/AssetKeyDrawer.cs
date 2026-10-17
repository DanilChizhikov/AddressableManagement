using UnityEditor;
using UnityEngine;

namespace DTech.AddressableManagement.Editor
{
	[CustomPropertyDrawer(typeof(AssetKey), true)]
	internal sealed class AssetKeyDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SerializedProperty inner = FindInner(property);
			return inner == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(inner, label, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty inner = FindInner(property);
			if (inner == null)
			{
				EditorGUI.LabelField(position, label, new GUIContent("Unsupported key"));
				return;
			}

			EditorGUI.PropertyField(position, inner, label, true);
		}

		private static SerializedProperty FindInner(SerializedProperty property)
		{
			SerializedProperty inner = property.Copy();
			SerializedProperty end = property.GetEndProperty();
			if (!inner.NextVisible(true) || SerializedProperty.EqualContents(inner, end))
			{
				return null;
			}

			return inner;
		}
	}
}