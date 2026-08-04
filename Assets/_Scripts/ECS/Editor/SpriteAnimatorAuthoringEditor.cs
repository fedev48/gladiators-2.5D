using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpriteAnimatorAuthoring))]
public class SpriteAnimatorAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (target == null) return;
        var auth = (SpriteAnimatorAuthoring)target;
        serializedObject.Update();

        string[] names = auth.animations != null
            ? auth.animations.Select((c, i) => $"[{i}] {(c != null ? $"{c.animation} {c.animationDirection}" : "")}").ToArray()
            : new string[0];

        var animsProp = serializedObject.FindProperty("animations");
        animsProp.isExpanded = EditorGUILayout.Foldout(animsProp.isExpanded, "Animations", true);
        if (animsProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            int newSize = EditorGUILayout.DelayedIntField("Size", animsProp.arraySize);
            if (newSize != animsProp.arraySize) animsProp.arraySize = newSize;

            for (int i = 0; i < animsProp.arraySize; i++)
                DrawClip(animsProp.GetArrayElementAtIndex(i), i, names);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialAnimation"), new GUIContent("Initial Animation"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("flipPivotOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraYAngle"));

#if SYSTEM_DEBUG
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        var overrideProp = serializedObject.FindProperty("debugOverride");
        EditorGUILayout.PropertyField(overrideProp, new GUIContent("Debug Override"));

        if (overrideProp.boolValue && names.Length > 0)
        {
            var animProp = serializedObject.FindProperty("debugAnimation");
            animProp.intValue = Mathf.Clamp(animProp.intValue, 0, names.Length - 1);
            animProp.intValue = EditorGUILayout.Popup("Animation", animProp.intValue, names);
        }
#endif

        serializedObject.ApplyModifiedProperties();
    }

    static void DrawClip(SerializedProperty clip, int index, string[] allNames)
    {
        var animationProp  = clip.FindPropertyRelative("animation");
        var directionProp  = clip.FindPropertyRelative("animationDirection");
        var isOverrideProp = clip.FindPropertyRelative("isOverride");
        var overrideToProp = clip.FindPropertyRelative("overrideTo");

        string animName = animationProp.enumValueIndex >= 0 ? animationProp.enumDisplayNames[animationProp.enumValueIndex] : "?";
        string dirName  = directionProp.enumValueIndex >= 0 ? directionProp.enumDisplayNames[directionProp.enumValueIndex] : "?";
        string label = $"[{index}] {animName} {dirName}";

        clip.isExpanded = EditorGUILayout.Foldout(clip.isExpanded, label, true);
        if (!clip.isExpanded) return;

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(animationProp, new GUIContent("Animation"));
        EditorGUILayout.PropertyField(directionProp, new GUIContent("Direction"));

        var flipProp = clip.FindPropertyRelative("flip");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(isOverrideProp, new GUIContent("Override"));
        if (isOverrideProp.boolValue && allNames.Length > 0)
        {
            overrideToProp.intValue = EditorGUILayout.Popup(
                Mathf.Clamp(overrideToProp.intValue, 0, allNames.Length - 1), allNames);
        }
        EditorGUILayout.EndHorizontal();

        if (isOverrideProp.boolValue)
            EditorGUILayout.PropertyField(flipProp, new GUIContent("Flip X"));

        if (!isOverrideProp.boolValue)
        {
            EditorGUILayout.PropertyField(clip.FindPropertyRelative("frames"), true);
            EditorGUILayout.PropertyField(clip.FindPropertyRelative("fps"));

            var hitFrameProp = clip.FindPropertyRelative("hitFrame");
            int frameCount   = clip.FindPropertyRelative("frames").arraySize;
            hitFrameProp.intValue = Mathf.Clamp(
                EditorGUILayout.IntField(new GUIContent("Hit Frame", "-1 = el clip no golpea"), hitFrameProp.intValue),
                -1, Mathf.Max(frameCount - 1, -1));
        }

        EditorGUI.indentLevel--;
    }
}
