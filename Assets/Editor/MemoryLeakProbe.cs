using System;
using System.Collections;
using System.Reflection;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression probe for runtime-created Unity objects that are not scene assets and therefore
/// must be released explicitly. Run from the menu or with -executeMethod MemoryLeakProbe.ProbeBatch.
/// </summary>
public static class MemoryLeakProbe
{
    private const int Cycles = 64;

    [MenuItem("Tools/Project Art of All/Probe Runtime Memory Leaks")]
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
            int tooltipResult = ProbeTooltipStatics();
            int ringResult = ProbeRingMeshes();
            int pathResult = ProbePathMaterials();
            bool passed = tooltipResult == 0 && ringResult == 0 && pathResult == 0;

            string message = string.Concat(
                "[memory-probe] tooltip=",
                tooltipResult == 0 ? "clean" : "leaked",
                " hover=",
                CountStaticList("ProjectArtOfAll.Board.CardHoverLift", "Active"),
                " ringMeshes=",
                ringResult,
                " pathMaterials=",
                pathResult,
                " cycles=",
                Cycles);
            if (passed)
            {
                Debug.Log(message);
                exitCode = 0;
            }
            else
            {
                Debug.LogError(message);
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

    private static int ProbeTooltipStatics()
    {
        GameObject canvasObject = new GameObject(
            "MemoryProbeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        RectTransform canvas = (RectTransform)canvasObject.transform;
        CardTooltipView view = CardTooltipView.Create(
            canvas,
            BoardHudOverlayFactory.CreateFont());
        CardTooltipHost.Register(view);

        CardDefinition card = CardDatabase.Instance.Get("crowbar");
        CardTooltipHost.Show(
            canvas,
            BoardHudOverlayFactory.CreateFont(),
            card,
            new Vector2(300f, 300f));
        CardHoverLift.Apply(view.transform as RectTransform);

        // EditMode does not dispatch OnDestroy for ordinary MonoBehaviours; invoke the same
        // callback the runtime will call, then verify that its cleanup releases every reference.
        InvokePrivate(view, "OnDestroy");
        UnityEngine.Object.DestroyImmediate(view.gameObject);
        UnityEngine.Object.DestroyImmediate(canvasObject);

        int leaked = 0;
        leaked += IsStaticNull("ProjectArtOfAll.Board.CardTooltipHost", "view") ? 0 : 1;
        leaked += IsStaticNull("ProjectArtOfAll.Board.CardTooltipHost", "hostedCanvas") ? 0 : 1;
        leaked += IsStaticNull("ProjectArtOfAll.Board.CardTooltipHost", "visibleCard") ? 0 : 1;
        leaked += CountStaticList("ProjectArtOfAll.Board.CardHoverLift", "Active");
        return leaked;
    }

    private static int ProbeRingMeshes()
    {
        int leaked = 0;
        for (int index = 0; index < Cycles; index++)
        {
            GameObject ringObject = new GameObject(
                "MemoryProbeRing",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            ScreenCenteredRing ring = ringObject.AddComponent<ScreenCenteredRing>();
            ring.RebuildMesh();
            Mesh mesh = ringObject.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(ringObject);
            if (mesh != null)
            {
                leaked++;
            }
        }

        return leaked;
    }

    private static int ProbePathMaterials()
    {
        int leaked = 0;
        for (int index = 0; index < Cycles; index++)
        {
            GameObject linesObject = new GameObject("MemoryProbePathLines");
            BoardPathLines lines = linesObject.AddComponent<BoardPathLines>();
            Material material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "MemoryProbePathLine"
            };
            SetPrivateField(lines, "material", material);
            InvokePrivate(lines, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(linesObject);
            if (material != null)
            {
                leaked++;
            }
        }

        return leaked;
    }

    private static bool IsStaticNull(string typeName, string fieldName)
    {
        Type type = FindType(typeName);
        FieldInfo field = type == null
            ? null
            : type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                "Missing static field " + typeName + "." + fieldName);
        }

        return field.GetValue(null) == null;
    }

    private static int CountStaticList(string typeName, string fieldName)
    {
        Type type = FindType(typeName);
        FieldInfo field = type == null
            ? null
            : type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                "Missing static list " + typeName + "." + fieldName);
        }

        return field.GetValue(null) is ICollection collection
            ? collection.Count
            : -1;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                "Missing instance field " + target.GetType().Name + "." + fieldName);
        }

        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException(
                "Missing instance method " + target.GetType().Name + "." + methodName);
        }

        method.Invoke(target, null);
    }

    private static Type FindType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int index = 0; index < assemblies.Length; index++)
        {
            Type type = assemblies[index].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
