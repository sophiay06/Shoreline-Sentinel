// Assets/Scripts/Editor/MappingControllerEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MappingTool;

[CustomEditor(typeof(MappingController))]
public class MappingControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ctrl = (MappingController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Designer Tool", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Active Preset"))
            {
                ctrl.LoadActivePreset();
                EditorUtility.SetDirty(ctrl);
            }

            if (GUILayout.Button("Set Recommended Defaults"))
            {
                ctrl.SetRecommendedDefaultsAll();
                EditorUtility.SetDirty(ctrl);
            }
        }

        EditorGUILayout.HelpBox(
            "Tip: For the study, use presets (DS/Naive/Expert) and reload them before runs.\n" +
            "For exploration, set condition to Custom, then edit the bindings list.",
            MessageType.Info
        );
    }
}
#endif
