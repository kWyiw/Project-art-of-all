using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offline codex verification: checks all 94 definitions are rendered, exercises
/// event/type filters, and writes a 1920x1080 preview PNG.
/// </summary>
public static class CardCodexPreview
{
    private const int Width = 1920;
    private const int Height = 1080;

    [MenuItem("Tools/Project Art of All/Preview Card Codex")]
    private static void RenderFromMenu()
    {
        Render();
    }

    public static void RenderBatch()
    {
        Render();
    }

    private static void Render()
    {
        GameObject cameraObject = new GameObject("CodexPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.036f, 0.044f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = Height / 200f;
        camera.cullingMask = -1;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture target = new RenderTexture(Width, Height, 24);
        target.Create();
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject(
            "CodexPreviewCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial" },
            22);

        CardCodexView view = CardCodexView.Create(canvasRect, font);
        view.Show();

        int allCount = CountTiles(view.gameObject);
        if (allCount != 94)
        {
            Fail(view, cameraObject, target, "[codex-preview] all rows=" + allCount);
            return;
        }

        InvokeButton(view.gameObject, "AcqEvent");
        int eventCount = CountTiles(view.gameObject);
        InvokeButton(view.gameObject, "AcqAll");
        InvokeButton(view.gameObject, "Type_Disease");
        int diseaseCount = CountTiles(view.gameObject);
        InvokeButton(view.gameObject, "TypeAll");

        if (eventCount != 1 || diseaseCount < 1 || CountTiles(view.gameObject) != 94)
        {
            Fail(
                view,
                cameraObject,
                target,
                $"[codex-preview] event={eventCount} disease={diseaseCount}");
            return;
        }

        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        string path = "_tmp/card_codex_preview.png";
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Debug.Log(
            $"[codex-preview] wrote {path} all={allCount} event={eventCount} disease={diseaseCount}");

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);
    }

    private static int CountTiles(GameObject root)
    {
        return root
            .GetComponentsInChildren<Transform>(true)
            .Count(transform => transform.name.StartsWith("Codex_", System.StringComparison.Ordinal));
    }

    private static void InvokeButton(GameObject root, string name)
    {
        Button button = root
            .GetComponentsInChildren<Button>(true)
            .FirstOrDefault(candidate => candidate.name == name);
        button?.onClick.Invoke();
    }

    private static void Fail(
        CardCodexView view,
        GameObject cameraObject,
        RenderTexture target,
        string message)
    {
        Debug.LogError(message);
        Object.DestroyImmediate(view.gameObject);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);
        EditorApplication.Exit(1);
    }
}
