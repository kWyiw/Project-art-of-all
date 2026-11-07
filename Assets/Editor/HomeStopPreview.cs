using System.IO;
using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把「经过自己的家」二选一浮层单独渲染成 PNG，用来在没有真人的情况下检查版式。
/// 用法：菜单 Tools/Preview Home Stop Prompt，或批处理
/// `Unity.exe -batchmode -quit -projectPath . -executeMethod HomeStopPreview.RenderBatch`。
/// </summary>
public static class HomeStopPreview
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int UiLayer = 5;

    [MenuItem("Tools/Preview Home Stop Prompt")]
    public static void RenderFromMenu()
    {
        Render("_tmp/homestop_preview.png");
    }

    public static void RenderBatch()
    {
        Render("_tmp/homestop_preview.png");
    }

    private static void Render(string path)
    {
        GameObject cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.18f, 0.17f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = Height / 200f;
        camera.cullingMask = 1 << UiLayer;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture target = new RenderTexture(Width, Height, 24);
        target.Create();
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject(
            "PreviewCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
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

        // 假背景：让半透明遮罩看起来像盖在棋盘上。
        GameObject board = new GameObject(
            "Board",
            typeof(RectTransform),
            typeof(Image));
        board.transform.SetParent(canvasRect, false);
        board.layer = UiLayer;
        RectTransform boardRect = board.GetComponent<RectTransform>();
        boardRect.anchorMin = Vector2.zero;
        boardRect.anchorMax = Vector2.one;
        boardRect.offsetMin = Vector2.zero;
        boardRect.offsetMax = Vector2.zero;
        Image boardImage = board.GetComponent<Image>();
        boardImage.color = new Color(0.42f, 0.45f, 0.44f, 1f);
        boardImage.raycastTarget = false;

        BoardHomeStopView view = BoardHudOverlayFactory.CreateHomeStopView(canvasRect, font);
        view.Overlay.SetActive(true);

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
        Debug.Log("[preview] wrote " + path);

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(canvasObject);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);
    }
}
