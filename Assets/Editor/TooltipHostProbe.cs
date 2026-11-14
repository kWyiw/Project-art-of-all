using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 回归探针：悬停窗在场景里是"烤"在 GameplayHUD/BakedOverlays 下的，而调用方传的是根画布。
/// 这两个口径一旦不一致，CardTooltipHost 会以为换了画布、把自己的实例销毁掉，于是悬停再也不显示。
/// 批处理 `-executeMethod TooltipHostProbe.Run` 跑一遍即可。
/// </summary>
public static class TooltipHostProbe
{
    [MenuItem("Tools/Probe Card Tooltip Host")]
    public static void Run()
    {
        GameObject canvasObject = new GameObject(
            "ProbeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        RectTransform bakedOverlays = new GameObject("BakedOverlays", typeof(RectTransform))
            .GetComponent<RectTransform>();
        bakedOverlays.SetParent(canvasRect, false);

        RectTransform bandRoot = new GameObject("CardBand", typeof(RectTransform))
            .GetComponent<RectTransform>();
        bandRoot.SetParent(canvasRect, false);

        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial" }, 22);

        CardTooltipView baked = CardTooltipView.Create(bakedOverlays, font);
        CardTooltipHost.Register(baked);

        CardDefinition crowbar = CardDatabase.Instance.Get("crowbar");

        // 第一次悬停：模拟卡槽带扫到一张牌。
        CardTooltipHost.Show(CardTooltipHost.CanvasRect(bandRoot), font, crowbar, new Vector2(900f, 800f));
        Object[] views = Object.FindObjectsOfType<CardTooltipView>(true);
        bool firstOk = views.Length == 1 && ((CardTooltipView)views[0]).IsShown;

        // 第二次悬停另一张牌：如果实例被销毁过，这一次会彻底没有窗口。
        CardTooltipHost.Show(
            CardTooltipHost.CanvasRect(bandRoot),
            font,
            CardDatabase.Instance.Get("lighter"),
            new Vector2(880f, 780f));
        views = Object.FindObjectsOfType<CardTooltipView>(true);
        bool secondOk = views.Length == 1 && ((CardTooltipView)views[0]).IsShown;

        CardTooltipHost.Hide();

        string message = string.Concat(
            "[probe] tooltips=", views.Length.ToString(),
            " first=", firstOk ? "shown" : "missing",
            " second=", secondOk ? "shown" : "missing");
        if (firstOk && secondOk)
        {
            Debug.Log(message);
        }
        else
        {
            Debug.LogError(message);
        }

        Object.DestroyImmediate(canvasObject);
    }
}
