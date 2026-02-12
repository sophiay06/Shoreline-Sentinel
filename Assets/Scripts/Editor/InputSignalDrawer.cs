#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MappingTool;

[CustomPropertyDrawer(typeof(InputSignal))]
public class InputSignalDrawer : PropertyDrawer
{
    private static bool IsDebugOnly(InputSignal s)
    {
        var fi = typeof(InputSignal).GetField(s.ToString());
        return fi != null && fi.GetCustomAttribute<DebugOnlyAttribute>() != null;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var current = (InputSignal)property.enumValueIndex;

        var visible = Enum.GetValues(typeof(InputSignal))
            .Cast<InputSignal>()
            .Where(s => !IsDebugOnly(s))
            .ToList();

        bool currentHidden = IsDebugOnly(current);
        if (currentHidden && !visible.Contains(current))
            visible.Insert(0, current);

        var display = visible.Select(s =>
            (currentHidden && EqualityComparer<InputSignal>.Default.Equals(s, current))
                ? $"(Hidden) {s}"
                : s.ToString()
        ).ToArray();

        int currentIndex = Mathf.Max(0, visible.IndexOf(current));
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, display);

        property.enumValueIndex = (int)visible[newIndex];
    }
}
#endif
