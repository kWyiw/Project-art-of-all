using System.IO;
using ProjectArtOfAll.Board;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 离线预览「局内弹窗让开卡槽带」的版式：底部画一条与卡槽带等高、等宽的假带子，
/// 再把奖励面板按 <see cref="BoardHudOverlayFactory.FitAboveCardBand"/> 压到它上方，
/// 用来确认遮罩不再盖住带子、面板也没顶出画布。
/// 菜单 Tools/Preview Modal Layout，或批处理 -executeMethod ModalLayoutPreview.RenderBatch。
/// </summary>
public static class ModalLayoutPreview
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int UiLayer = 5;

    [UnityEditor.MenuItem("Tools/Preview Modal Layout")]
    public static void RenderFromMenu()
    {
        Render("_tmp/modal_layout_expanded.png", ExpandedBandHeight, false);
        Render("_tmp/modal_layout_collapsed.png", CollapsedBandHeight, true);
    }

    public static void RenderBatch()
    {
        Render("_tmp/modal_layout_expanded.png", ExpandedBandHeight, false);
        Render("_tmp/modal_layout_collapsed.png", CollapsedBandHeight, true);
    }

    private const float CollapsedBandHeight = 42f;

    private static float ExpandedBandHeight => Mathf.Clamp(Height * 0.42f, 272f, 560f);

    private static void Render(string path, float bandHeight, bool collapsed)
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

        GameObject board = new GameObject("Board", typeof(RectTransform), typeof(Image));
        board.transform.SetParent(canvasRect, false);
        board.layer = UiLayer;
        Stretch(board.GetComponent<RectTransform>());
        Image boardImage = board.GetComponent<Image>();
        boardImage.color = new Color(0.42f, 0.45f, 0.44f, 1f);
        boardImage.raycastTarget = false;

        // 卡槽带：宽度取 ApplyScreenLayout 的同一个公式，高度取"展开后"的值。
        float bandWidth = Mathf.Clamp(Width * 0.78f, 720f, 1560f);
        GameObject band = new GameObject("FakeCardBand", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(canvasRect, false);
        band.layer = UiLayer;
        RectTransform bandRect = band.GetComponent<RectTransform>();
        bandRect.anchorMin = new Vector2(0.5f, 0f);
        bandRect.anchorMax = new Vector2(0.5f, 0f);
        bandRect.pivot = new Vector2(0.5f, 0f);
        bandRect.anchoredPosition = Vector2.zero;
        bandRect.sizeDelta = new Vector2(bandWidth, bandHeight);
        Image bandImage = band.GetComponent<Image>();
        bandImage.color = new Color(0.06f, 0.09f, 0.10f, 0.99f);
        bandImage.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(band.transform, false);
        labelObject.layer = UiLayer;
        Text bandLabel = labelObject.GetComponent<Text>();
        bandLabel.font = font;
        bandLabel.fontSize = 20;
        bandLabel.fontStyle = FontStyle.Normal;
        bandLabel.text = collapsed
            ? "卡槽带（已收起：弹窗回到接近居中）"
            : "卡槽带（展开：弹窗被抬到带子上方）";
        bandLabel.color = new Color(0.94f, 0.93f, 0.88f, 1f);
        bandLabel.alignment = TextAnchor.UpperLeft;
        bandLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        bandLabel.raycastTarget = false;
        RectTransform labelRect = bandLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(20f, -18f);
        labelRect.sizeDelta = new Vector2(bandWidth - 40f, 28f);

        BoardHudRewardChoiceView reward = BoardHudOverlayFactory.CreateRewardChoiceView(canvasRect, font);
        reward.Overlay.SetActive(true);
        reward.Title.text = "奖励 · 选一张";
        reward.Subtitle.text = "免费获得 · 背包 9/10c · 装不下的牌会置灰";
        BoardHudOverlayFactory.FitAboveCardBand(reward.Overlay, bandHeight);

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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
