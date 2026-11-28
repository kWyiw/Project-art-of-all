using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Imports the authored standee with the project's Built-in settings and
    /// builds the material/prefab assets consumed by BoardStandeeVisual.
    /// </summary>
    public static class CreateRedHoodStandeeAssets
    {
        private const string Folder = "Assets/Resources/Standees/RedHood";
        private const string FbxPath = Folder + "/red_hood_acrylic_standee.fbx";
        private const string TexturePath = Folder + "/red_hood_illustration.png";
        private const string PrintMaterialPath = Folder + "/MAT_Standee_Print.mat";
        private const string AcrylicMaterialPath =
            Folder + "/MAT_Standee_Acrylic.mat";
        private const string BaseTopMaterialPath =
            Folder + "/MAT_Standee_BaseTop.mat";
        private const string BaseEdgeMaterialPath =
            Folder + "/MAT_Standee_BaseEdge.mat";
        private const string PrefabPath = Folder + "/PF_RedHoodStandee.prefab";
        private const string ResultPath =
            "_tmp/redhood-standee-assets.result.txt";

        [MenuItem("Tools/Project Art of All/Build Red Hood Standee Assets")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before rebuilding the red-hood standee assets.");
            }

            EnsureFolder(Folder);
            ConfigureModelImporter();
            ConfigureTextureImporter();

            Texture2D illustration =
                AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (illustration == null || modelAsset == null)
            {
                throw new InvalidOperationException(
                    "The standee FBX or illustration texture did not import.");
            }

            Material print = CreateOrUpdatePrintMaterial(illustration);
            Material acrylic = CreateOrUpdateTransparentMaterial(
                AcrylicMaterialPath,
                "MAT_Standee_Acrylic",
                new Color(0.94f, 0.97f, 0.98f, 0.12f),
                0.94f);
            Material baseTop = CreateOrUpdateTransparentMaterial(
                BaseTopMaterialPath,
                "MAT_Standee_BaseTop",
                new Color(0.18f, 0.47f, 0.52f, 0.52f),
                0.9f);
            Material baseEdge = CreateOrUpdateTransparentMaterial(
                BaseEdgeMaterialPath,
                "MAT_Standee_BaseEdge",
                new Color(0.055f, 0.22f, 0.27f, 0.88f),
                0.84f);

            AssetDatabase.SaveAssets();
            BuildPrefab(
                modelAsset,
                print,
                acrylic,
                baseTop,
                baseEdge);

            StringBuilder result = new StringBuilder();
            result.AppendLine("PASS");
            result.AppendLine("fbx=" + FbxPath);
            result.AppendLine("texture=" + TexturePath);
            result.AppendLine("prefab=" + PrefabPath);
            result.AppendLine("print_queue=" + print.renderQueue);
            result.AppendLine("acrylic_queue=" + acrylic.renderQueue);
            WriteResult(result.ToString());
            Debug.Log("[Standee] Built material and prefab assets.");
        }

        private static void ConfigureModelImporter()
        {
            AssetDatabase.ImportAsset(
                FbxPath,
                ImportAssetOptions.ForceSynchronousImport);
            ModelImporter importer =
                AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create a model importer for " + FbxPath + ".");
            }

            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter()
        {
            AssetDatabase.ImportAsset(
                TexturePath,
                ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer =
                AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create a texture importer for " + TexturePath + ".");
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdatePrintMaterial(Texture2D illustration)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Standard shader is unavailable.");
            }

            Material material = LoadOrCreateMaterial(
                PrintMaterialPath,
                shader,
                "MAT_Standee_Print");
            material.shader = shader;
            material.mainTexture = illustration;
            material.SetTexture("_MainTex", illustration);
            material.SetColor("_Color", new Color(0.82f, 0.82f, 0.82f, 1f));
            material.SetFloat("_Mode", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.SetFloat("_Cutoff", 0.1f);
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = 2450;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.02f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateTransparentMaterial(
            string path,
            string materialName,
            Color color,
            float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Standard shader is unavailable.");
            }

            Material material = LoadOrCreateMaterial(path, shader, materialName);
            material.shader = shader;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(
            string path,
            Shader shader,
            string materialName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        private static void BuildPrefab(
            GameObject modelAsset,
            Material print,
            Material acrylic,
            Material baseTop,
            Material baseEdge)
        {
            GameObject instance = Object.Instantiate(modelAsset);
            try
            {
                instance.name = "RedHoodStandee";
                Renderer printRenderer = FindRenderer(instance, "Illustration_Plane");
                Renderer acrylicRenderer = FindRenderer(instance, "Acrylic_Panel");
                Renderer baseRenderer = FindRenderer(instance, "Round_Base");
                if (printRenderer == null ||
                    acrylicRenderer == null ||
                    baseRenderer == null)
                {
                    throw new InvalidOperationException(
                        "The FBX must contain Illustration_Plane, Acrylic_Panel and Round_Base.");
                }

                printRenderer.sharedMaterial = print;
                printRenderer.shadowCastingMode = ShadowCastingMode.Off;
                printRenderer.receiveShadows = false;

                acrylicRenderer.sharedMaterial = acrylic;
                acrylicRenderer.shadowCastingMode = ShadowCastingMode.Off;
                acrylicRenderer.receiveShadows = false;

                baseRenderer.sharedMaterials =
                    new[] { baseTop, baseEdge };
                baseRenderer.shadowCastingMode = ShadowCastingMode.On;
                baseRenderer.receiveShadows = true;

                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Renderer FindRenderer(GameObject root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                    transforms[index].name,
                    objectName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[index].GetComponent<Renderer>();
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator < 0)
            {
                throw new InvalidOperationException("Invalid asset folder: " + path);
            }

            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void WriteResult(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? "_tmp");
            File.WriteAllText(ResultPath, content);
        }
    }
}
