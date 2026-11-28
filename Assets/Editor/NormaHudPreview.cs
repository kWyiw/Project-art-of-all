using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Offline preview and regression probe for the Norma goal labels and the post-upgrade
/// choice modal. The probe opens Map1 so it exercises the serialized buttons, not
/// runtime-created lookalikes.
/// </summary>
public static class NormaHudPreview
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int UiLayer = 5;
    private const string PreviewPath = "_tmp/norma_choice_preview.png";
    private const string Map1Path = "Assets/Scenes/Map1.unity";

    [MenuItem("Tools/Preview Norma Choice")]
    public static void RenderFromMenu()
    {
        Render(PreviewPath);
    }

    public static void RenderBatch()
    {
        Render(PreviewPath);
    }

    [MenuItem("Tools/Project Art of All/Probe Norma HUD")]
    public static void ProbeFromMenu()
    {
        Probe(false);
    }

    public static void ProbeBatch()
    {
        Probe(true);
    }

    private static void Probe(bool exitEditor)
    {
        int exitCode = 1;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MatchController controller = BuildController();
            MatchPlayerState player = controller.State.Players[0];
            SetInternal(player, "Level", 3);
            SetInternal(player, "Shards", 12);
            SetInternal(player, "Wins", 2);
            SetInternal(player, "Norma", NormaType.Shard);

            bool labels = ProbeCurrentGoalLabels(controller, player);
            bool buttons = ProbeBakedChoiceButtons(player);
            if (labels && buttons)
            {
                Debug.Log("[norma-hud] labels=yes shardButton=yes winsButton=yes");
                exitCode = 0;
            }
            else
            {
                Debug.LogError(
                    $"[norma-hud] labels={(labels ? "yes" : "no")} "
                    + $"shardButton={(buttons ? "yes" : "no")}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }

    private static bool ProbeCurrentGoalLabels(
        MatchController controller,
        MatchPlayerState player)
    {
        GameObject canvasObject = new GameObject(
            "NormaProbeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        RectTransform canvas = canvasObject.GetComponent<RectTransform>();
        canvas.sizeDelta = new Vector2(Width, Height);

        Font font = BoardHudOverlayFactory.CreateFont();
        PlayerCornerPanels corners = PlayerCornerPanels.Create(canvas, font, null);
        corners.Refresh(controller.State, player.Id);
        Text economy = corners.GetComponentsInChildren<Text>(true)
            .First(text => text.gameObject.name == "Economy");
        bool cornerOk = economy.text.Contains("目标 碎片")
            && economy.text.Contains("<color=#F2C14E>12/60</color>");

        BoardFieldInfoPanel field = BoardFieldInfoPanel.Create(canvas, font);
        field.Refresh(controller.State, "Norma Probe", player.Id);
        Text norma = field.GetComponentsInChildren<Text>(true)
            .First(text => text.gameObject.name == "Norma");
        bool fieldOk = norma.text.Contains("升级条件：碎片 12/60")
            && norma.text.Contains("Lv.3 → Lv.4");

        UnityEngine.Object.DestroyImmediate(canvasObject);
        return cornerOk && fieldOk;
    }

    private static bool ProbeBakedChoiceButtons(MatchPlayerState player)
    {
        EditorSceneManager.OpenScene(Map1Path, OpenSceneMode.Single);
        BoardHudController hud = UnityEngine.Object
            .FindObjectsOfType<BoardHudController>(true)
            .FirstOrDefault();
        if (hud == null)
        {
            return false;
        }

        NormaType chosen = NormaType.Wins;
        bool shardShown = hud.ShowNormaChoice(player, value => chosen = value);
        Button shard = FindButton("ShardNormaButton");
        bool shardClicked = shardShown && shard != null;
        if (shardShown && shard != null)
        {
            shard.onClick.Invoke();
        }

        bool shardOk = shardClicked && chosen == NormaType.Shard;

        chosen = NormaType.Shard;
        bool winsShown = hud.ShowNormaChoice(player, value => chosen = value);
        Button wins = FindButton("WinsNormaButton");
        bool winsClicked = winsShown && wins != null;
        if (winsShown && wins != null)
        {
            wins.onClick.Invoke();
        }

        bool winsOk = winsClicked && chosen == NormaType.Wins;
        if (!shardOk || !winsOk)
        {
            Debug.LogError(
                $"[norma-hud] shardShown={shardShown} shardFound={shard != null} "
                + $"winsShown={winsShown} winsFound={wins != null}");
        }

        return shardOk && winsOk;
    }

    private static Button FindButton(string objectName)
    {
        Button[] buttons = UnityEngine.Object.FindObjectsOfType<Button>(true);
        for (int index = 0; index < buttons.Length; index++)
        {
            if (buttons[index].gameObject.name == objectName)
            {
                return buttons[index];
            }
        }

        return null;
    }

    private static MatchController BuildController()
    {
        MapGraph graph = MapGraph.Create(
            new[]
            {
                new MapNode(0, TileType.Home, 0, 1),
                new MapNode(1, TileType.Reward, -1, 2),
                new MapNode(2, TileType.Home, 1, 3),
                new MapNode(3, TileType.Reward, -1, 4),
                new MapNode(4, TileType.Home, 2, 5),
                new MapNode(5, TileType.Reward, -1, 6),
                new MapNode(6, TileType.Home, 3, 7),
                new MapNode(7, TileType.Reward, -1, 0)
            },
            playerCount: 4);
        return new MatchController(graph, 5150UL);
    }

    private static void SetInternal(
        MatchPlayerState player,
        string propertyName,
        object value)
    {
        PropertyInfo property = typeof(MatchPlayerState).GetProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException("Missing player property " + propertyName);
        }

        property.SetValue(player, value);
    }

    private static void Render(string path)
    {
        GameObject cameraObject = new GameObject("PreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.16f, 0.17f, 1f);
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
        BoardNormaChoiceView view = BoardHudOverlayFactory.CreateNormaChoiceView(
            canvasRect,
            BoardHudOverlayFactory.CreateFont());
        view.Title.text = "升级成功 · P1 升到 Lv.3";
        view.Body.text =
            "下一级从 Lv.3 升到 Lv.4。\n"
            + "只能选择一条路线，选定后到下次升级前不能更改。\n"
            + "碎片 Norma：持有 40 碎片（当前 26）\n"
            + "胜场 Norma：累计 6 胜场（当前 2）";
        view.ShardLabel.text = "碎片 Norma\n目标：持有 40 碎片";
        view.WinsLabel.text = "胜场 Norma\n目标：累计 6 胜场";
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

        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(canvasObject);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
