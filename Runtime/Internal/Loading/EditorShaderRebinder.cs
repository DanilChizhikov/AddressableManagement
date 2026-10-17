using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal static class EditorShaderRebinder
    {
        [Conditional("UNITY_EDITOR")]
        public static void Rebind(Object asset)
        {
#if UNITY_EDITOR
            switch (asset)
            {
                case GameObject gameObject:
                    RebindHierarchy(gameObject);
                    break;
                case Material material:
                    RebindMaterial(material);
                    break;
            }
#endif
        }

        [Conditional("UNITY_EDITOR")]
        public static void Rebind(Scene scene)
        {
#if UNITY_EDITOR
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                RebindHierarchy(root);
            }
#endif
        }

#if UNITY_EDITOR
        private static void RebindHierarchy(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    RebindMaterial(material);
                }
            }

#if DTECH_ADDRESSABLES_UGUI
            foreach (UnityEngine.UI.Graphic graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                RebindMaterial(graphic.material);
            }
#endif
        }

        private static void RebindMaterial(Material material)
        {
            if (material == null || material.shader == null || UnityEditor.AssetDatabase.Contains(material))
            {
                return;
            }

            Shader shader = Shader.Find(material.shader.name);
            if (shader == null || shader == material.shader)
            {
                return;
            }

            int renderQueue = material.renderQueue;
            material.shader = shader;
            material.renderQueue = renderQueue;
        }
#endif
    }
}
