using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CutsceneDialogueClip))]
public class CutsceneDialogueClipEditor : Editor
{
    private SerializedProperty dialogueLine;
    private SerializedProperty speakerType;
    private SerializedProperty lineText;
    private SerializedProperty legacySpeaker;
    private SerializedProperty legacyText;
    private SerializedProperty useCustomOffset;
    private SerializedProperty customOffset;
    private SerializedProperty overrideBubbleSize;
    private SerializedProperty bubbleSize;
    private SerializedProperty disableTypewriter;
    private SerializedProperty typewriterCharactersPerSecond;
    private SerializedProperty typewriterStartDelay;
    private SerializedProperty overrideTextLayout;
    private SerializedProperty fontSize;
    private SerializedProperty textOffset;
    private SerializedProperty textPadding;

    private void OnEnable()
    {
        SerializedProperty template = serializedObject.FindProperty("template");
        if (template == null)
            return;

        dialogueLine = template.FindPropertyRelative("dialogueLine");
        if (dialogueLine != null)
        {
            speakerType = dialogueLine.FindPropertyRelative("speakerType");
            lineText = dialogueLine.FindPropertyRelative("text");
        }

        legacySpeaker = template.FindPropertyRelative("speaker");
        legacyText = template.FindPropertyRelative("text");
        useCustomOffset = template.FindPropertyRelative("useCustomOffset");
        customOffset = template.FindPropertyRelative("customOffset");
        overrideBubbleSize = template.FindPropertyRelative("overrideBubbleSize");
        bubbleSize = template.FindPropertyRelative("bubbleSize");
        disableTypewriter = template.FindPropertyRelative("disableTypewriter");
        typewriterCharactersPerSecond = template.FindPropertyRelative("typewriterCharactersPerSecond");
        typewriterStartDelay = template.FindPropertyRelative("typewriterStartDelay");
        overrideTextLayout = template.FindPropertyRelative("overrideTextLayout");
        fontSize = template.FindPropertyRelative("fontSize");
        textOffset = template.FindPropertyRelative("textOffset");
        textPadding = template.FindPropertyRelative("textPadding");
    }

    public override void OnInspectorGUI()
    {
        if (speakerType == null || lineText == null || useCustomOffset == null || customOffset == null ||
            overrideBubbleSize == null || bubbleSize == null ||
            disableTypewriter == null || typewriterCharactersPerSecond == null || typewriterStartDelay == null ||
            overrideTextLayout == null || fontSize == null || textOffset == null || textPadding == null)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        MigrateLegacyDialogueLineIfNeeded();

        EditorGUILayout.PropertyField(speakerType, new GUIContent("Speaker Type"));

        EditorGUILayout.LabelField("Text");
        lineText.stringValue = EditorGUILayout.TextArea(lineText.stringValue, GUILayout.MinHeight(80f));
        SyncLegacyDialogueLine();

        EditorGUILayout.Space();
        DrawBubblePositionFields();

        EditorGUILayout.Space();
        DrawTypewriterFields();

        EditorGUILayout.Space();
        bool wasCustomTextLayout = overrideTextLayout.boolValue;
        EditorGUILayout.PropertyField(overrideTextLayout, new GUIContent("Custom Text Layout"));
        if (!wasCustomTextLayout && overrideTextLayout.boolValue)
            InitializeTextLayoutDefaults();

        if (overrideTextLayout.boolValue)
        {
            EditorGUILayout.PropertyField(fontSize, new GUIContent("Font Size"));
            EditorGUILayout.PropertyField(textOffset, new GUIContent("Text Position Offset"));
            DrawTextPaddingFields();
        }

        if (serializedObject.ApplyModifiedProperties())
            EditorUtility.SetDirty(target);
    }

    private void DrawBubblePositionFields()
    {
        EditorGUILayout.LabelField("Bubble Position");

        bool wasCustomBubblePosition = useCustomOffset.boolValue;
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(useCustomOffset, new GUIContent("Custom Bubble Position"));
        if (!wasCustomBubblePosition && useCustomOffset.boolValue)
            customOffset.vector3Value = GetDefaultBubbleOffset();

        if (useCustomOffset.boolValue)
        {
            Vector3 position = customOffset.vector3Value;
            position.x = EditorGUILayout.FloatField("X", position.x);
            position.y = EditorGUILayout.FloatField("Y", position.y);
            position.z = EditorGUILayout.FloatField("Z", position.z);
            customOffset.vector3Value = position;
        }

        bool wasCustomBubbleSize = overrideBubbleSize.boolValue;
        EditorGUILayout.PropertyField(overrideBubbleSize, new GUIContent("Custom Bubble Size"));
        if (!wasCustomBubbleSize && overrideBubbleSize.boolValue)
            bubbleSize.vector2Value = GetDefaultBubbleSize();

        if (overrideBubbleSize.boolValue)
        {
            Vector2 size = bubbleSize.vector2Value;
            size.x = Mathf.Max(1f, EditorGUILayout.FloatField("Width", size.x));
            size.y = Mathf.Max(1f, EditorGUILayout.FloatField("Height", size.y));
            bubbleSize.vector2Value = size;
        }
        EditorGUI.indentLevel--;
    }

    private void DrawTextPaddingFields()
    {
        Vector4 padding = textPadding.vector4Value;

        EditorGUILayout.LabelField("Text Padding");
        EditorGUI.indentLevel++;
        padding.x = EditorGUILayout.FloatField("Left", padding.x);
        padding.y = EditorGUILayout.FloatField("Top", padding.y);
        padding.z = EditorGUILayout.FloatField("Right", padding.z);
        padding.w = EditorGUILayout.FloatField("Bottom", padding.w);
        EditorGUI.indentLevel--;

        textPadding.vector4Value = padding;
    }

    private void DrawTypewriterFields()
    {
        bool useTypewriter = !disableTypewriter.boolValue;
        bool updatedUseTypewriter = EditorGUILayout.Toggle(new GUIContent("Typewriter Text"), useTypewriter);
        disableTypewriter.boolValue = !updatedUseTypewriter;

        if (!updatedUseTypewriter)
            return;

        if (typewriterCharactersPerSecond.floatValue <= 0f)
            typewriterCharactersPerSecond.floatValue = 28f;

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(typewriterCharactersPerSecond, new GUIContent("Characters Per Second"));
        EditorGUILayout.PropertyField(typewriterStartDelay, new GUIContent("Start Delay"));
        EditorGUI.indentLevel--;
    }

    private void InitializeTextLayoutDefaults()
    {
        if (fontSize.floatValue <= 0f)
            fontSize.floatValue = 40f;

        if (textPadding.vector4Value == Vector4.zero)
            textPadding.vector4Value = new Vector4(86f, 58f, 86f, 74f);
    }

    private Vector3 GetDefaultBubbleOffset()
    {
        return speakerType.enumValueIndex == (int)SpeakerType.NPC
            ? new Vector3(0f, 2.2f, 0f)
            : new Vector3(0f, 2.4f, 0f);
    }

    private void MigrateLegacyDialogueLineIfNeeded()
    {
        if (legacySpeaker == null || legacyText == null || speakerType == null || lineText == null)
            return;

        if (!string.IsNullOrEmpty(lineText.stringValue) || string.IsNullOrEmpty(legacyText.stringValue))
            return;

        speakerType.enumValueIndex = legacySpeaker.enumValueIndex;
        lineText.stringValue = legacyText.stringValue;
    }

    private void SyncLegacyDialogueLine()
    {
        if (legacySpeaker != null)
            legacySpeaker.enumValueIndex = speakerType.enumValueIndex;

        if (legacyText != null)
            legacyText.stringValue = lineText.stringValue ?? string.Empty;
    }

    private static Vector2 GetDefaultBubbleSize()
    {
        return new Vector2(620f, 190f);
    }
}
