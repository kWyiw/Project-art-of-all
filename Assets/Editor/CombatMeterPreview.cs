using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Match;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把战斗回放的「血条 + 状态数字条」单独渲染成 PNG，用来看版式（药丸宽度、字号、上色、留白）。
/// 用法：菜单 Tools/Preview Combat Meters，或批处理
/// `Unity.exe -batchmode -quit -projectPath . -executeMethod CombatMeterPreview.RenderBatch`。
/// </summary>
public static class CombatMeterPreview
{
    private const int Width = 1600;
    private const int Height = 900;

    [MenuItem("Tools/Preview Combat Meters")]
    public static void RenderFromMenu()
    {
        Render("_tmp/combat_meter_preview.png");
    }

    public static void RenderBatch()
    {
        Render("_tmp/combat_meter_preview.png");
    }

    private static void Render(string path)
    {
        GameObject cameraObject = new GameObject("CombatPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.13f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = Height / 200f;
        camera.cullingMask = ~0;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture target = new RenderTexture(Width, Height, 24);
        target.Create();
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject(
            "CombatPreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(Width, Height);
        Font font = BoardHudOverlayFactory.CreateFont();

        BattleSnapshot battle = new BattleSnapshot(
            BuildSide(0, 2, new[] { "crowbar", "nuclear_water", "oil_barrel", "hardtack" }),
            BuildSide(1, 2, new[] { "car_door", "lighter", "snow_purifier", "molotov" }));

        CombatSimulation simulation = new CombatSimulation(battle, 20240521UL);
        CombatResult result = simulation.RunToCompletion();
        CombatResolvedEvent resolved = new CombatResolvedEvent(0, 1, result);

        CombatPlaybackView view = CombatPlaybackView.Create(canvasRect, font);
        try
        {
            view.Play(resolved, "你", "对手", true, null, 0f);
        }
        catch (Exception exception)
        {
            // 编辑器不在播放模式时协程起不来，内容已经建好了，忽略。
            Debug.Log("[preview] playback coroutine skipped: " + exception.GetType().Name);
        }

        ApplyStatuses(view, 0, 34, 12, 7, 5);
        ApplyStatuses(view, 1, 2, 0, 18, 0);

        Canvas.ForceUpdateCanvases();
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
        Debug.Log("[preview] wrote " + path + " (health " + battle.Left.MaxHealth + "/" + battle.Right.MaxHealth + ")");

        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(canvasObject);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static CombatantSnapshot BuildSide(int playerId, int level, string[] cardIds)
    {
        List<CardInstance> cards = new List<CardInstance>();
        int uid = 1;
        foreach (string cardId in cardIds)
        {
            if (CardDatabase.Instance.TryGet(cardId, out CardDefinition definition))
            {
                cards.Add(new CardInstance(uid++, definition));
            }
        }

        return new CombatantSnapshot(playerId, level, cards);
    }

    private static void ApplyStatuses(
        CombatPlaybackView view,
        int sideIndex,
        int shield,
        int poison,
        int burn,
        int regeneration)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type viewType = typeof(CombatPlaybackView);
        FieldInfo field = viewType.GetField(sideIndex == 0 ? "selfSide" : "foeSide", Flags);
        object side = field != null ? field.GetValue(view) : null;
        if (side == null)
        {
            Debug.LogError("[preview] side missing: " + sideIndex);
            return;
        }

        Type sideType = side.GetType();
        sideType.GetField("Health")?.SetValue(side, 61 - sideIndex * 20);
        sideType.GetField("Shield")?.SetValue(side, shield);
        sideType.GetField("Poison")?.SetValue(side, poison);
        sideType.GetField("Burn")?.SetValue(side, burn);
        sideType.GetField("Regeneration")?.SetValue(side, regeneration);
        viewType.GetMethod("RefreshMeters", Flags)?.Invoke(view, null);
    }
}
