using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectArtOfAll.Editor
{
    /// <summary>Validates the authored FBX using the same runtime visual path as Map1/Map2.</summary>
    // Run once after the authored resources import, or from the Tools menu.
    // This validation never saves a gameplay scene.
    public static class ValidateRedHoodStandee
    {
        private const string FbxPath =
            "Assets/Resources/Standees/RedHood/red_hood_acrylic_standee.fbx";
        private const string TexturePath =
            "Assets/Resources/Standees/RedHood/red_hood_illustration.png";
        private const string PrefabPath =
            "Assets/Resources/Standees/RedHood/PF_RedHoodStandee.prefab";
        private const string Map1Path = "Assets/Scenes/Map1.unity";
        private const string ResultPath = "_tmp/redhood-standee-v6.result.txt";
        private const string PreviewPath = "_tmp/redhood-standee-v6-preview.png";
        private const string MapFullPreviewPath = "_tmp/map1-standee-v6-full.png";
        private const string MapPreviewPath = "_tmp/map1-standee-v6-detail.png";
        private static readonly Vector3 PreviewOrigin = new Vector3(10000f, 0f, 0f);

        [MenuItem("Tools/Project Art of All/Validate Red Hood Standee")]
        public static void ValidateFromMenu()
        {
            ValidateAndRender(renderMapDetail: true);
        }

        private static void ValidateAndRender(bool renderMapDetail)
        {
            GameObject tokenObject = null;
            Camera camera = null;
            GameObject[] lightingObjects = Array.Empty<GameObject>();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException(
                        "Exit Play Mode before validating the standee.");
                }

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (modelAsset == null || texture == null || prefab == null)
                {
                    throw new InvalidOperationException(
                        "FBX, illustration texture or standee prefab is missing from " +
                        "the Unity asset database.");
                }

                Renderer[] importedRenderers =
                    modelAsset.GetComponentsInChildren<Renderer>(true);
                if (importedRenderers.Length != 3)
                {
                    throw new InvalidOperationException(
                        "Expected three imported renderers, found " +
                        importedRenderers.Length + ".");
                }

                Bounds authoredBounds = ValidateAuthoredAssets(prefab, texture);
                tokenObject = new GameObject("Player_1_Red_Preview");
                tokenObject.hideFlags = HideFlags.HideAndDontSave;
                tokenObject.transform.position = PreviewOrigin;
                tokenObject.transform.localScale = new Vector3(0.58f, 1.35f, 0.58f);
                BoardPlayerToken token = tokenObject.AddComponent<BoardPlayerToken>();
                token.Configure(0, 0, 0);

                camera = CreateCamera();
                lightingObjects = CreateLighting();
                BoardStandeeVisual.ApplyMainCharacter(
                    new Dictionary<int, BoardPlayerToken> { [0] = token },
                    camera);
                SetHideFlagsRecursively(tokenObject);

                Transform visual = tokenObject.transform.Find("RedHoodStandee");
                if (visual == null)
                {
                    throw new InvalidOperationException(
                        "Runtime standee replacement did not create its visual root.");
                }

                Renderer[] visualRenderers =
                    visual.GetComponentsInChildren<Renderer>(true);
                if (visualRenderers.Length != 3)
                {
                    throw new InvalidOperationException(
                        "Runtime standee has " + visualRenderers.Length +
                        " renderers instead of three.");
                }

                Renderer runtimeAcrylic = FindRenderer(
                    visual.gameObject,
                    "Acrylic_Panel");
                if (runtimeAcrylic == null ||
                    runtimeAcrylic.material.renderQueue !=
                    (int)RenderQueue.Geometry + 1)
                {
                    throw new InvalidOperationException(
                        "Runtime acrylic material is not sorted behind the print.");
                }

                Bounds bounds = CalculateBounds(visualRenderers);
                if (Mathf.Abs(bounds.size.y - BoardStandeeVisual.GameplayHeight) > 0.03f)
                {
                    throw new InvalidOperationException(
                        "Runtime standee height is " + bounds.size.y +
                        ", expected " + BoardStandeeVisual.GameplayHeight + ".");
                }

                RenderPreview(
                    camera,
                    PreviewPath,
                    900,
                    1200);
                if (renderMapDetail)
                {
                    RenderMapDetail();
                }
                Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? "_tmp");
                StringBuilder result = new StringBuilder();
                result.AppendLine("PASS");
                result.AppendLine("fbx=" + FbxPath);
                result.AppendLine("texture=" + TexturePath);
                result.AppendLine("imported_renderers=" + importedRenderers.Length);
                result.AppendLine("runtime_renderers=" + visualRenderers.Length);
                result.AppendLine(
                    "authored_bounds=" +
                    authoredBounds.size.x.ToString("F4") + "," +
                    authoredBounds.size.y.ToString("F4") + "," +
                    authoredBounds.size.z.ToString("F4"));
                result.AppendLine(
                    "runtime_bounds=" +
                    bounds.size.x.ToString("F4") + "," +
                    bounds.size.y.ToString("F4") + "," +
                    bounds.size.z.ToString("F4"));
                result.AppendLine("preview=" + PreviewPath);
                if (renderMapDetail)
                {
                    result.AppendLine("map_full_preview=" + MapFullPreviewPath);
                    result.AppendLine("map_preview=" + MapPreviewPath);
                }
                File.WriteAllText(ResultPath, result.ToString());
                Debug.Log("[Standee] Import validation passed. " + PreviewPath);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("_tmp");
                File.WriteAllText(
                    ResultPath,
                    "FAIL\n" + exception + "\n");
                Debug.LogException(exception);
            }
            finally
            {
                if (tokenObject != null)
                {
                    Object.DestroyImmediate(tokenObject);
                }

                if (camera != null)
                {
                    Object.DestroyImmediate(camera.gameObject);
                }

                for (int index = 0; index < lightingObjects.Length; index++)
                {
                    if (lightingObjects[index] != null)
                    {
                        Object.DestroyImmediate(lightingObjects[index]);
                    }
                }
            }
        }

        private static Bounds ValidateAuthoredAssets(
            GameObject prefab,
            Texture2D texture)
        {
            GameObject instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Renderer print = FindRenderer(instance, "Illustration_Plane");
                Renderer acrylic = FindRenderer(instance, "Acrylic_Panel");
                Renderer baseRenderer = FindRenderer(instance, "Round_Base");
                if (print == null || acrylic == null || baseRenderer == null)
                {
                    throw new InvalidOperationException(
                        "Prefab hierarchy is missing one of the three authored renderers.");
                }

                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = CalculateBounds(renderers);
                if (Mathf.Abs(bounds.size.y - 0.24f) > 0.02f)
                {
                    throw new InvalidOperationException(
                        "Authored standee height is " + bounds.size.y +
                        ", expected approximately 0.24 m.");
                }

                ValidatePresentation(print, acrylic, baseRenderer, texture);
                ValidateImporters();
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ValidatePresentation(
            Renderer print,
            Renderer acrylic,
            Renderer baseRenderer,
            Texture2D texture)
        {
            Material printMaterial = print.sharedMaterial;
            Material acrylicMaterial = acrylic.sharedMaterial;
            if (printMaterial == null ||
                printMaterial.shader == null ||
                printMaterial.shader.name != "Standard")
            {
                throw new InvalidOperationException(
                    "Illustration_Plane is not using Standard.");
            }

            if (printMaterial.mainTexture != texture ||
                Mathf.Abs(printMaterial.GetFloat("_Mode") - 1f) > 0.001f ||
                Mathf.Abs(printMaterial.GetFloat("_Cutoff") - 0.1f) > 0.001f ||
                !printMaterial.IsKeywordEnabled("_ALPHATEST_ON") ||
                printMaterial.renderQueue != 2450)
            {
                throw new InvalidOperationException(
                    "Print texture, cutoff or render queue is incorrect.");
            }

            if (acrylicMaterial == null ||
                acrylicMaterial.shader == null ||
                acrylicMaterial.shader.name != "Standard")
            {
                throw new InvalidOperationException(
                    "Acrylic_Panel is not using the Standard shader.");
            }

            Color acrylicColor = acrylicMaterial.GetColor("_Color");
            if (acrylicColor.a < 0.08f ||
                acrylicColor.a > 0.15f ||
                acrylicMaterial.GetFloat("_Glossiness") < 0.85f ||
                acrylicMaterial.renderQueue != 3000)
            {
                throw new InvalidOperationException(
                    "Acrylic alpha, smoothness or render queue is incorrect.");
            }

            if (baseRenderer.sharedMaterials.Length != 2)
            {
                throw new InvalidOperationException(
                    "Round_Base must have separate top and edge materials.");
            }

            if (print.shadowCastingMode != ShadowCastingMode.Off ||
                acrylic.shadowCastingMode != ShadowCastingMode.Off ||
                baseRenderer.shadowCastingMode != ShadowCastingMode.On)
            {
                throw new InvalidOperationException(
                    "Standee shadow casting modes are incorrect.");
            }
        }

        private static void ValidateImporters()
        {
            ModelImporter modelImporter =
                AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (modelImporter == null ||
                Mathf.Abs(modelImporter.globalScale - 1f) > 0.001f ||
                modelImporter.importNormals != ModelImporterNormals.Import ||
                modelImporter.importTangents != ModelImporterTangents.CalculateMikk ||
                modelImporter.meshCompression != ModelImporterMeshCompression.Off ||
                modelImporter.isReadable)
            {
                throw new InvalidOperationException(
                    "FBX import scale, normals, tangents, compression or readability " +
                    "does not match the standee contract.");
            }

            TextureImporter textureImporter =
                AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (textureImporter == null ||
                textureImporter.textureType != TextureImporterType.Default ||
                !textureImporter.sRGBTexture ||
                !textureImporter.alphaIsTransparency ||
                textureImporter.wrapMode != TextureWrapMode.Clamp ||
                textureImporter.maxTextureSize != 4096 ||
                !textureImporter.mipmapEnabled)
            {
                throw new InvalidOperationException(
                    "Illustration texture import settings do not match the standee contract.");
            }
        }

        private static Renderer FindRenderer(GameObject root, string objectName)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (string.Equals(
                    renderers[index].gameObject.name,
                    objectName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return renderers[index];
                }
            }

            return null;
        }

        private static void SetHideFlagsRecursively(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("PreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.018f, 0.024f, 1f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            cameraObject.transform.position =
                PreviewOrigin + new Vector3(1.15f, 0.55f, -2.8f);
            cameraObject.transform.LookAt(PreviewOrigin);
            return camera;
        }

        private static GameObject[] CreateLighting()
        {
            GameObject keyObject = new GameObject("PreviewKey");
            keyObject.hideFlags = HideFlags.HideAndDontSave;
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.9f, 0.82f);
            key.intensity = 1.4f;
            keyObject.transform.rotation = Quaternion.Euler(42f, -36f, 0f);

            GameObject fillObject = new GameObject("PreviewFill");
            fillObject.hideFlags = HideFlags.HideAndDontSave;
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.74f, 0.86f, 1f);
            fill.intensity = 0.55f;
            fillObject.transform.rotation = Quaternion.Euler(25f, 135f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.2f, 0.24f);
            return new[] { keyObject, fillObject };
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void RenderMapDetail()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene mapScene = EditorSceneManager.OpenScene(Map1Path, OpenSceneMode.Single);
            BoardGameplayController controller =
                Object.FindObjectOfType<BoardGameplayController>();
            Camera sourceCamera = Camera.main;
            if (controller == null || sourceCamera == null)
            {
                throw new InvalidOperationException(
                    "Map1 is missing its gameplay controller or main camera.");
            }

            BoardPlayerToken[] tokens =
                controller.GetComponentsInChildren<BoardPlayerToken>(true);
            Dictionary<int, BoardPlayerToken> tokenLookup =
                new Dictionary<int, BoardPlayerToken>();
            for (int index = 0; index < tokens.Length; index++)
            {
                tokenLookup[tokens[index].PlayerId] = tokens[index];
            }

            if (!tokenLookup.TryGetValue(0, out BoardPlayerToken mainToken))
            {
                throw new InvalidOperationException("Map1 has no player 1 token.");
            }

            GameObject cameraObject = new GameObject("StandeeDetailCamera");
            Camera detailCamera = cameraObject.AddComponent<Camera>();
            detailCamera.CopyFrom(sourceCamera);
            detailCamera.orthographic = true;
            detailCamera.orthographicSize =
                BoardCameraController.MinimumZoom;
            Quaternion detailRotation = Quaternion.Euler(
                BoardCameraController.CloseUpCameraPitch,
                sourceCamera.transform.eulerAngles.y,
                0f);
            Vector3 focus = mainToken.transform.position;
            focus.y = BoardCameraController.CloseUpFocusHeight;
            Vector3 forward = detailRotation * Vector3.forward;
            float downward = Mathf.Max(0.001f, -forward.y);
            float distance =
                (BoardCameraController.CloseUpCameraHeight - focus.y) / downward;
            detailCamera.transform.SetPositionAndRotation(
                focus - forward * distance,
                detailRotation);

            BoardStandeeVisual.ApplyMainCharacter(tokenLookup, detailCamera);
            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            for (int index = 0; index < canvases.Length; index++)
            {
                if (canvases[index].gameObject.scene == mapScene)
                {
                    canvases[index].gameObject.SetActive(false);
                }
            }

            RenderPreview(
                sourceCamera,
                MapFullPreviewPath,
                1600,
                900);
            RenderPreview(
                detailCamera,
                MapPreviewPath,
                1200,
                900);
            Object.DestroyImmediate(cameraObject);

            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        private static void RenderPreview(
            Camera camera,
            string path,
            int width,
            int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                Directory.CreateDirectory("_tmp");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
