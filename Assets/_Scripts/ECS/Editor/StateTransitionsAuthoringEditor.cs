using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StateTransitionsAuthoring))]
public class StateTransitionsAuthoringEditor : Editor
{
    static Type[]   stateTypes;
    static string[] stateNames;

    void OnEnable()
    {
        if (stateTypes != null) return;

        stateTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(type => type.IsValueType && !type.IsAbstract && typeof(IFSMState).IsAssignableFrom(type))
            .OrderBy(type => type.Name)
            .ToArray();

        stateNames = stateTypes.Select(type => type.Name).ToArray();
    }

    static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(type => type != null); }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (stateTypes.Length == 0)
        {
            EditorGUILayout.HelpBox("No states found. States must implement IFSMState.", MessageType.Warning);
            return;
        }

        SerializedProperty transitionsProp = serializedObject.FindProperty("transitions");

        for (int i = 0; i < transitionsProp.arraySize; i++)
            DrawTransition(transitionsProp, i);

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add transition"))
        {
            transitionsProp.arraySize++;
            SerializedProperty added = transitionsProp.GetArrayElementAtIndex(transitionsProp.arraySize - 1);
            added.FindPropertyRelative("fromState").stringValue = stateTypes[0].AssemblyQualifiedName;
            added.FindPropertyRelative("toState").stringValue   = stateTypes[0].AssemblyQualifiedName;
            added.FindPropertyRelative("conditions").intValue         = 0;
            added.FindPropertyRelative("rangeThreshold").floatValue   = 0f;
            added.FindPropertyRelative("healthThreshold").floatValue  = 0f;
            added.FindPropertyRelative("enemiesThreshold").intValue   = 0;
            added.FindPropertyRelative("priority").intValue           = 0;
            added.isExpanded = true;
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawTransition(SerializedProperty transitionsProp, int index)
    {
        SerializedProperty transition = transitionsProp.GetArrayElementAtIndex(index);
        SerializedProperty fromProp   = transition.FindPropertyRelative("fromState");
        SerializedProperty toProp     = transition.FindPropertyRelative("toState");
        SerializedProperty condProp   = transition.FindPropertyRelative("conditions");

        string label = $"[{index}] {ShortName(fromProp.stringValue)} -> {ShortName(toProp.stringValue)}";

        EditorGUILayout.BeginHorizontal();
        transition.isExpanded = EditorGUILayout.Foldout(transition.isExpanded, label, true);
        if (GUILayout.Button("x", GUILayout.Width(20)))
        {
            transitionsProp.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (!transition.isExpanded) return;

        EditorGUI.indentLevel++;

        DrawStatePopup("From", fromProp);
        DrawStatePopup("To", toProp);

        condProp.intValue = (int)(StateCondition)EditorGUILayout.EnumFlagsField(
            new GUIContent("Conditions", "All checked conditions must be met at once"),
            (StateCondition)condProp.intValue);

        StateCondition conditions = (StateCondition)condProp.intValue;

        DrawThreshold(transition, "rangeThreshold",   "Range",       conditions, StateCondition.TargetInRange | StateCondition.TargetOutOfRange);
        DrawThreshold(transition, "healthThreshold",  "Health",      conditions, StateCondition.HealthBelow);
        DrawThreshold(transition, "enemiesThreshold", "Enemies Around", conditions, StateCondition.EnemiesAroundAbove);

        EditorGUILayout.PropertyField(transition.FindPropertyRelative("priority"));

        EditorGUI.indentLevel--;
    }

    static void DrawThreshold(SerializedProperty transition, string field, string label, StateCondition conditions, StateCondition usedBy)
    {
        using (new EditorGUI.DisabledScope((conditions & usedBy) == 0))
            EditorGUILayout.PropertyField(transition.FindPropertyRelative(field), new GUIContent(label));
    }

    void DrawStatePopup(string label, SerializedProperty stateProp)
    {
        int current = Array.FindIndex(stateTypes, type => type.AssemblyQualifiedName == stateProp.stringValue);
        int picked  = EditorGUILayout.Popup(label, Mathf.Max(current, 0), stateNames);

        if (picked != current)
            stateProp.stringValue = stateTypes[picked].AssemblyQualifiedName;
    }

    static string ShortName(string assemblyQualifiedName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName)) return "?";

        int comma = assemblyQualifiedName.IndexOf(',');
        return comma > 0 ? assemblyQualifiedName.Substring(0, comma) : assemblyQualifiedName;
    }
}
