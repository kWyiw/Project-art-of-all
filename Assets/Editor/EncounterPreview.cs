using System.IO;
using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把遭遇战面板（带「无视，继续走」出口的版本）单独渲染成 PNG，用来检查两个按钮的排布。
/// 用法：菜单 Tools/Preview Encounter Prompt，或批处理
/// `Unity.exe -batchmode -quit -projectPath . -executeMethod EncounterPreview.RenderBatch`。
/// </summary>
public static class EncounterPreview
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int UiLayer = 5;

    [MenuItem("Tools/Preview Encounter Prompt")]
    public static void RenderFromMenu()
    {
        Render("_tmp/encounter_preview.png");
    }

    public static void RenderBatch()
    {
        Render("_tmp/encounter_preview.png");
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

        BoardHudEncounterViews view =
            BoardHudOverlayFactory.CreateEncounterOverlay(canvasRect, font);
        view.Overlay.SetActive(true);

        view.Subtitle.text =
            "玩家 1 经过玩家 2 所在的地块。开战会停在这里结算；无视他就继续走完这一轮。";
        view.AttackerName.text = "玩家 1";
        view.AttackerStats.text = "Lv.3 · 装配 6c · 碎片 12 · 胜场 1";
        view.AttackerLoadout.text = "撬棍 1c · 核废水 2c · 压缩饼干 1c · 汽车门板 2c";
        view.DefenderName.text = "玩家 2";
        view.DefenderStats.text = "Lv.4 · 装配 5c · 碎片 8 · 胜场 2";
        view.DefenderLoadout.text = "打火机 1c · 油桶 2c · 燃烧瓶 1c · 雪水净化器 1c";
        view.AcceptLabel.text = "开战";
        view.IgnoreLabel.text = "无视，继续走";
        view.IgnoreButton.gameObject.SetActive(true);
        view.IgnoreButton.interactable = true;

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
