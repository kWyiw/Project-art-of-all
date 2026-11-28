using System.Collections.Generic;
using System.IO;
using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Offline verification for the StartMenu: renders the normal and first-item
/// hover states at 1920x1080 into _tmp so layout can be checked without play mode.
/// </summary>
public static class StartMenuPreview
{
    private const int Width = 1920;
    private const int Height = 1080;

    [MenuItem("Tools/Project Art of All/Preview Start Menu")]
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
        GameObject cameraObject = new GameObject("StartMenuPreviewCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = Height / 200f;
        camera.cullingMask = -1;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        RenderTexture target = new RenderTexture(Width, Height, 24);
        target.Create();
        camera.targetTexture = target;

        GameObject menuObject = new GameObject("StartMenuPreview");
        LeftMenuColumn menu = menuObject.AddComponent<LeftMenuColumn>();
        menu.BuildMenuForEditor();

        RectTransform canvasRect = menu.EditorCanvasRect;
        Canvas canvas = canvasRect.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasRect.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        Canvas.ForceUpdateCanvases();
        Transform background = canvasRect.Find("Background");
        StartMenuParallaxBackground parallax =
            background != null ? background.GetComponent<StartMenuParallaxBackground>() : null;
        if (parallax != null)
        {
            parallax.Configure(canvasRect, 1.08f, 18f, 0.12f);
        }

        Transform snowTransform = canvasRect.Find("SnowField");
        StartMenuSnowField snow =
            snowTransform != null
                ? snowTransform.GetComponent<StartMenuSnowField>()
                : null;

        StartMenuOptionView[] options =
            menu.GetComponentsInChildren<StartMenuOptionView>(true);
        if (options.Length == 0)
        {
            Debug.LogError("[startmenu-preview] no StartMenuOptionView found.");
            Cleanup(menuObject, cameraObject, target);
            EditorApplication.Exit(1);
            return;
        }

        if (snow != null)
        {
            RenderSnowTime(canvas, camera, target, snow, 0f,
                "_tmp/startmenu-snow-0s.png");
            RenderSnowTime(canvas, camera, target, snow, 2f,
                "_tmp/startmenu-snow-2s.png");
            RenderSnowTime(canvas, camera, target, snow, 5f,
                "_tmp/startmenu-snow-5s.png");
            RenderSnowTime(canvas, camera, target, snow, 0f,
                "_tmp/startmenu-unity-normal.png");
            Debug.Log(
                $"[startmenu-snow] particles={snow.ParticleCount} "
                + $"layers={snow.FarParticleCount}/{snow.MidParticleCount}/"
                + $"{snow.NearParticleCount} wrote 0s/2s/5s previews");
        }
        else
        {
            Canvas.ForceUpdateCanvases();
            RenderToFile(camera, target, "_tmp/startmenu-unity-normal.png");
            Debug.LogWarning("[startmenu-snow] SnowField not found.");
        }

        Canvas.ForceUpdateCanvases();
        if (!ValidateHitArea(canvas, camera, options[0]))
        {
            Cleanup(menuObject, cameraObject, target);
            EditorApplication.Exit(1);
            return;
        }

        options[0].SetHoverProgress(0.55f);
        Canvas.ForceUpdateCanvases();
        RenderToFile(camera, target, "_tmp/startmenu-unity-hover-mid.png");

        options[0].SetHoverInstant(true);
        Canvas.ForceUpdateCanvases();
        RenderToFile(camera, target, "_tmp/startmenu-unity-hover.png");

        Debug.Log(
            $"[startmenu-preview] options={options.Length} wrote "
            + "normal, hover-mid and hover previews");
        Cleanup(menuObject, cameraObject, target);
    }

    private static void RenderSnowTime(
        Canvas canvas,
        Camera camera,
        RenderTexture target,
        StartMenuSnowField snow,
        float time,
        string path)
    {
        snow.SetPreviewTime(time);
        Canvas.ForceUpdateCanvases();
        RenderToFile(camera, target, path);
    }

    private static bool ValidateHitArea(
        Canvas canvas,
        Camera camera,
        StartMenuOptionView option)
    {
        RectTransform hitArea = option.transform.Find("HitArea") as RectTransform;
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (hitArea == null || raycaster == null)
        {
            Debug.LogError("[startmenu-hit] missing HitArea or GraphicRaycaster.");
            return false;
        }

        Rect rect = hitArea.rect;
        bool inside = Hits(raycaster, camera, hitArea,
            new Vector2(rect.xMin + rect.width * 0.25f, rect.center.y));
        bool rightBlank = Hits(raycaster, camera, hitArea,
            new Vector2(rect.xMax + 40f, rect.center.y));
        bool slantCorner = Hits(raycaster, camera, hitArea,
            new Vector2(rect.xMax - 4f, rect.yMin + 4f));
        bool bottomLeft = Hits(raycaster, camera, hitArea,
            new Vector2(rect.xMin + 4f, rect.yMin + 4f));

        Debug.Log(
            $"[startmenu-hit] inside={inside} rightBlank={rightBlank} "
            + $"slantCorner={slantCorner} bottomLeft={bottomLeft}");
        return inside && !rightBlank && !slantCorner && bottomLeft;
    }

    private static bool Hits(
        GraphicRaycaster raycaster,
        Camera camera,
        RectTransform target,
        Vector2 localPoint)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            camera,
            target.TransformPoint(localPoint));
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };
        List<RaycastResult> results = new List<RaycastResult>();
        raycaster.Raycast(eventData, results);
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].gameObject == target.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    private static void RenderToFile(
        Camera camera,
        RenderTexture target,
        string path)
    {
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
        Object.DestroyImmediate(texture);
    }

    private static void Cleanup(
        GameObject menuObject,
        GameObject cameraObject,
        RenderTexture target)
    {
        Object.DestroyImmediate(menuObject);
        Object.DestroyImmediate(cameraObject);
        target.Release();
        Object.DestroyImmediate(target);
    }
}
