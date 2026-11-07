using System.IO;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把悬停卡牌面板单独渲染成 PNG，用来在没有真人的情况下检查版式（文字换行、间距、上色）。
/// 用法：菜单 Tools/Preview Card Tooltip，或批处理
/// `Unity.exe -batchmode -quit -projectPath . -executeMethod CardTooltipPreview.RenderBatch`。
/// </summary>
public static class CardTooltipPreview
{
    private const int Width = 900;
    private const int Height = 760;
    private const int UiLayer = 5;

    [MenuItem("Tools/Preview Card Tooltip")]
    public static void RenderFromMenu()
    {
        Render("_tmp/tooltip_preview.png", "generator");
    }

    public static void RenderBatch()
    {
        Render("_tmp/tooltip_preview.png", "generator");
        Render("_tmp/tooltip_preview_food.png", "hardtack");
        Render("_tmp/tooltip_preview_weapon.png", "shredder");
    }

    private static void Render(string path, string cardId)
    {
        if (!CardDatabase.Instance.TryGet(cardId, out CardDefinition definition))
        {
            Debug.LogError("[preview] unknown card: " + cardId);
            return;
        }

        GameObject cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.20f, 0.24f, 0.20f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = Height / 200f;
        camera.cullingMask = 1 << UiLayer;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture target = new RenderTexture(Width, Height, 24);
        target.Create();
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject(
            "PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = UiLayer;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial" }, 22);

        CardTooltipView view = CardTooltipView.Create(canvasRect, font);
        view.Show(
            definition,
            new Vector2(Width * 0.46f, Height * 0.42f),
            canvasRect,
            camera);

        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Debug.Log("[preview] wrote " + path + " for card " + cardId);

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);
    }
}
