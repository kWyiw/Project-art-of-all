using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectArtOfAll.Editor
{
    internal static class SetGameViewScaleToOne
    {
        [MenuItem("Tools/Project Art of All/Set Game View Scale to 1")]
        private static void ApplyOnReload()
        {
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            EditorApplication.ExecuteMenuItem("Window/Layouts/Default");
            EditorApplication.delayCall += ApplyScaleToOpenView;
        }

        private static void ApplyScaleToOpenView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                Debug.LogError("[Editor] Unity GameView type could not be resolved.");
                return;
            }

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(gameViewType);
            int updatedCount = 0;

            foreach (UnityEngine.Object windowObject in windows)
            {
                EditorWindow gameView = windowObject as EditorWindow;
                if (gameView == null)
                {
                    continue;
                }

                SerializedObject serializedGameView = new SerializedObject(gameView);
                SerializedProperty zoomArea = serializedGameView.FindProperty("m_ZoomArea");
                SerializedProperty scale = zoomArea?.FindPropertyRelative("m_Scale");
                if (scale == null)
                {
                    continue;
                }

                float logicalScale = 1f / EditorGUIUtility.pixelsPerPoint;
                scale.vector2Value = Vector2.one * logicalScale;
                serializedGameView.ApplyModifiedPropertiesWithoutUndo();
                PropertyInfo viewScaleProperty = gameView.GetType().GetProperty(
                    "viewMouseScale",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (viewScaleProperty != null && viewScaleProperty.CanWrite)
                {
                    viewScaleProperty.SetValue(gameView, 1f);
                }
                DumpSerializedProperties(serializedGameView);
                gameView.ShowTab();
                gameView.Focus();
                gameView.Repaint();
                updatedCount++;
            }

            if (updatedCount == 0)
            {
                Debug.LogError("[Editor] No GameView scale properties could be updated.");
                return;
            }

            Debug.Log($"[Editor] Game view scale set to 1x on {updatedCount} view(s).");
        }

        private static void DumpSerializedProperties(SerializedObject serializedObject)
        {
            StringBuilder output = new StringBuilder();
            SerializedProperty property = serializedObject.GetIterator();

            while (property.NextVisible(true))
            {
                string value = property.propertyType switch
                {
                    SerializedPropertyType.Float => property.floatValue.ToString("F4"),
                    SerializedPropertyType.Integer => property.intValue.ToString(),
                    SerializedPropertyType.Boolean => property.boolValue.ToString(),
                    SerializedPropertyType.Vector2 => property.vector2Value.ToString("F4"),
                    SerializedPropertyType.Vector3 => property.vector3Value.ToString("F4"),
                    SerializedPropertyType.Enum => property.enumValueIndex.ToString(),
                    _ => string.Empty
                };

                output.AppendLine($"{property.propertyPath} | {property.propertyType} | {value}");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.WriteAllText(
                Path.Combine(projectRoot, "Temp", "gameview_serialized_properties.txt"),
                output.ToString());

            string[] members = serializedObject.targetObject.GetType()
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(member =>
                    member.Name.IndexOf("scale", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    member.Name.IndexOf("zoom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    member.Name.IndexOf("fit", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(member => $"{member.MemberType} | {member.Name}")
                .ToArray();

            File.WriteAllLines(
                Path.Combine(projectRoot, "Temp", "gameview_related_members.txt"),
                members);

            StringBuilder values = new StringBuilder();
            Type windowType = serializedObject.targetObject.GetType();
            foreach (string propertyName in new[]
                     {
                         "minScale",
                         "maxScale",
                         "gameMouseScale",
                         "viewMouseScale"
                     })
            {
                PropertyInfo scaleProperty = windowType.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (scaleProperty != null && scaleProperty.CanRead)
                {
                    object value = scaleProperty.GetValue(serializedObject.targetObject);
                    values.AppendLine($"{propertyName} | {value}");
                }
            }

            foreach (string methodName in new[]
                     {
                         "ScaleThatFitsTargetInView",
                         "DefaultScaleForTargetInView"
                     })
            {
                MethodInfo method = windowType.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.GetParameters().Length == 0)
                {
                    object value = method.Invoke(serializedObject.targetObject, null);
                    values.AppendLine($"{methodName}() | {value}");
                }
            }

            File.WriteAllText(
                Path.Combine(projectRoot, "Temp", "gameview_scale_values.txt"),
                values.ToString());
        }
    }
}
