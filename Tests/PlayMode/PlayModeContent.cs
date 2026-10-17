using System;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#endif

namespace DTech.AddressableManagement.Tests.PlayMode
{
    internal sealed class PlayModeContent : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Folder = "Assets/DTechAddressableManagementTests";
        public const string HeroPath = Folder + "/Hero.prefab";
        public const string VillainPath = Folder + "/Villain.prefab";
        public const string IconPath = Folder + "/Icon.png";
        public const string ScenePath = Folder + "/Arena.unity";
        public const string HeroAddress = "dtech-am/hero";
        public const string VillainAddress = "dtech-am/villain";
        public const string IconAddress = "dtech-am/icon";
        public const string SceneAddress = "dtech-am/arena";
        public const string Label = "dtech-am-tests";
        public const string SceneMarker = "ArenaMarker";

        private const string GroupName = "DTech Addressable Management Tests";
        private const string CreatedSettingsKey = "DTech.AddressableManagement.Tests.CreatedSettings";
        private const string PlayModeIndexKey = "DTech.AddressableManagement.Tests.PlayModeIndex";

        public static string GuidOf(string path)
        {
#if UNITY_EDITOR
            return AssetDatabase.AssetPathToGUID(path);
#else
            return string.Empty;
#endif
        }

        public void Setup()
        {
#if UNITY_EDITOR
            bool createdSettings = AddressableAssetSettingsDefaultObject.Settings == null;
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            SessionState.SetBool(CreatedSettingsKey, createdSettings);
            SessionState.SetInt(PlayModeIndexKey, settings.ActivePlayModeDataBuilderIndex);

            CreateContent();
            AddressableAssetGroup group = settings.FindGroup(GroupName) ??
                    settings.CreateGroup(GroupName, false, false, true, null, typeof(BundledAssetGroupSchema));
            AddEntry(settings, group, HeroPath, HeroAddress, true);
            AddEntry(settings, group, VillainPath, VillainAddress, true);
            AddEntry(settings, group, IconPath, IconAddress, false);
            AddEntry(settings, group, ScenePath, SceneAddress, false);
            AssetDatabase.SaveAssets();

            settings.ActivePlayModeDataBuilderIndex = IndexOfPackedPlayMode(settings);
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Building the test content failed: {result.Error}");
            }
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                AddressableAssetGroup group = settings.FindGroup(GroupName);
                if (group != null)
                {
                    settings.RemoveGroup(group);
                }

                settings.ActivePlayModeDataBuilderIndex =
                        SessionState.GetInt(PlayModeIndexKey, settings.ActivePlayModeDataBuilderIndex);
                AddressableAssetSettings.CleanPlayerContent();
            }

            AssetDatabase.DeleteAsset(Folder);
            if (settings != null && SessionState.GetBool(CreatedSettingsKey, false))
            {
                AssetDatabase.DeleteAsset(settings.ConfigFolder);
            }

            AssetDatabase.SaveAssets();
#endif
        }

#if UNITY_EDITOR
        private static void CreateContent()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
            }

            SavePrefab(HeroPath, "Hero", true);
            SavePrefab(VillainPath, "Villain", false);
            SaveIcon();
            SaveScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SavePrefab(string path, string name, bool withCollider)
        {
            var gameObject = new GameObject(name);
            if (withCollider)
            {
                gameObject.AddComponent<BoxCollider>();
            }

            PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            Object.DestroyImmediate(gameObject);
        }

        private static void SaveIcon()
        {
            var texture = new Texture2D(4, 4);
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(IconPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static void SaveScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var marker = new GameObject(SceneMarker);
            SceneManager.MoveGameObjectToScene(marker, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void AddEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string path,
                string address, bool labelled)
        {
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            entry.address = address;
            if (labelled)
            {
                entry.SetLabel(Label, true, true);
            }
        }

        private static int IndexOfPackedPlayMode(AddressableAssetSettings settings)
        {
            for (int i = 0; i < settings.DataBuilders.Count; i++)
            {
                if (settings.DataBuilders[i] is BuildScriptPackedPlayMode)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("The Addressables settings have no 'Use Existing Build' play mode script.");
        }
#endif
    }
}
