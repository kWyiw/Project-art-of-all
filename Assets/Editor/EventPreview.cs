using System.IO;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Match;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把事件面板（R-EVT-002）单独渲染成 PNG，检查标题、三个选项与「走开」的排布。
/// 用法：菜单 Tools/Preview Event Panel，或批处理
/// `Unity.exe -batchmode -quit -projectPath . -executeMethod EventPreview.RenderBatch`。
/// </summary>
public static class EventPreview
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int UiLayer = 5;

    [MenuItem("Tools/Preview Event Panel")]
    public static void RenderFromMenu()
    {
        RenderAll();
    }

    public static void RenderBatch()
    {
        RenderAll();
    }

    private static void RenderAll()
    {
        Render(
            "_tmp/event_preview_expedition.png",
            MatchEventCatalog.Get(MatchEventKind.Expedition),
            shards: 7,
            ownedWidth: 4);
        Render(
            "_tmp/event_preview_drink.png",
            MatchEventCatalog.Get(MatchEventKind.MysteryDrink),
            shards: 2,
            ownedWidth: 8);
    }

    private static void Render(
        string path,
        MatchEventDefinition definition,
        int shards,
        int ownedWidth)
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

        BoardHudEventView view = BoardHudOverlayFactory.CreateEventView(canvasRect, font);
        view.Overlay.SetActive(true);

        view.Title.text = definition.Title;
        view.Subtitle.text = string.Concat(
            definition.Description,
            "  ·  碎片 ",
            shards.ToString(),
            "  ·  背包 ",
            ownedWidth.ToString(),
            "/",
            LoadoutRules.BagCapacity.ToString(),
            "c");

        for (int index = 0; index < view.Buttons.Length; index++)
        {
            bool visible = index < definition.Options.Count;
            view.Buttons[index].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            MatchEventOption option = definition.Options[index];
            bool affordable = shards >= option.ShardCost;
            bool fits = ownedWidth + option.CardWidth <= LoadoutRules.BagCapacity;

            view.Names[index].text = option.Label;
            view.Metas[index].text = option.Description;
            view.Details[index].text = BoardHudController.DescribeEventOption(option);
            view.Costs[index].text = affordable
                ? (option.ShardCost == 0
                    ? "免费"
                    : string.Concat("-", option.ShardCost.ToString(), " 碎片"))
                : string.Concat("差 ", (option.ShardCost - shards).ToString(), " 碎片");
            view.Costs[index].color = affordable
                ? new Color(0.92f, 0.69f, 0.27f, 1f)
                : new Color(0.86f, 0.44f, 0.40f, 1f);

            if (option.CardWidth > 0 && !fits)
            {
                view.Metas[index].text = string.Concat(
                    option.Description,
                    "  ·  背包装不下（需要 ",
                    option.CardWidth.ToString(),
                    "c）");
            }

            view.Buttons[index].interactable = affordable && fits;
        }

        view.LeaveLabel.text = "走开";

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
