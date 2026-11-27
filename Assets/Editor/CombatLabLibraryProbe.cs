using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 回归探针：模拟「场景里烤进去的卡牌库是旧卡表」的情况，确认
/// <see cref="CombatLabView"/> 会按 <see cref="CardDatabase"/> 重建卡牌库，
/// 让新增卡牌在模拟战斗里找得到。
///
/// 菜单：Tools/Probe Combat Lab Library；批处理：-executeMethod CombatLabLibraryProbe.Run。
/// </summary>
public static class CombatLabLibraryProbe
{
    [MenuItem("Tools/Probe Combat Lab Library")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        GameObject canvasObject = new GameObject(
            "ProbeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1600f, 900f);

        // 烤进场景的旧卡牌库：只有一行，等价于「卡表加牌但场景没重烤」。
        GameObject stale = new GameObject("CardLibrary", typeof(RectTransform));
        RectTransform staleRect = stale.GetComponent<RectTransform>();
        staleRect.SetParent(canvasRect, false);
        GameObject staleRow = new GameObject("Card_crowbar", typeof(RectTransform));
        staleRow.transform.SetParent(staleRect, false);

        GameObject labObject = new GameObject(
            "CombatLab",
            typeof(RectTransform),
            typeof(CanvasGroup));
        RectTransform labRect = labObject.GetComponent<RectTransform>();
        labRect.SetParent(canvasRect, false);
        labRect.anchorMin = Vector2.zero;
        labRect.anchorMax = Vector2.one;
        labRect.offsetMin = Vector2.zero;
        labRect.offsetMax = Vector2.zero;

        CombatLabView view = labObject.AddComponent<CombatLabView>();
        view.Bind();

        int expected = CardDatabase.Instance.AllDefinitions.Count;
        Transform library = labObject.transform.Find("CardLibrary");
        if (library == null)
        {
            Debug.LogError("[probe] the card library was never created.");
            EditorApplication.Exit(1);
            return;
        }

        Transform rows = library.Find("Viewport/Content");
        if (rows == null)
        {
            Debug.LogError("[probe] CardLibrary has no scroll content root.");
            EditorApplication.Exit(1);
            return;
        }

        string[] ids = rows
            .Cast<Transform>()
            .Select(row => row.name)
            .Where(name => name.StartsWith("Card_", System.StringComparison.Ordinal))
            .Select(name => name.Substring("Card_".Length))
            .ToArray();
        bool hasBarbed = ids.Contains("barbed_wire");
        bool hasRoadblock = ids.Contains("roadblock");
        bool hasRustedBlade = ids.Contains("rusted_blade");
        bool hasRustedBladePlus = ids.Contains("rusted_blade+");

        if (ids.Length != expected ||
            !hasBarbed ||
            !hasRoadblock ||
            !hasRustedBlade ||
            !hasRustedBladePlus)
        {
            Debug.LogError(
                $"[probe] rows={ids.Length} expected={expected} barbed={hasBarbed} "
                + $"roadblock={hasRoadblock} rusted_blade={hasRustedBlade} "
                + $"rusted_blade+={hasRustedBladePlus}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(
            $"[probe] rows={ids.Length} expected={expected} "
            + "barbed=yes roadblock=yes rusted_blade=yes rusted_blade+=yes");
        EditorApplication.Exit(0);
    }
}
