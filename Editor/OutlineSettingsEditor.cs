using UnityEditor;
using UnityEngine;

namespace VicotSoft.OutlineFeature.Editor
{

[CustomPropertyDrawer(typeof(OutlineFeature.OutlineSettings))]
public class OutlineSettingsEditor : PropertyDrawer
{
    const int PaddingBetweenFields = 4;

    private SerializedProperty theGlobalOneProp;
    private SerializedProperty colorProp;
    private SerializedProperty sizeProp;
    private SerializedProperty flagsProp;
    private SerializedProperty globalProp;
    private SerializedProperty alphaProp;
    private SerializedProperty passesProp;
    private SerializedProperty overrideHiddenProp;
    private SerializedProperty hiddenMatProp;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EditorGUI.LabelField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), "Outline settings");
        position.y += EditorGUIUtility.singleLineHeight + PaddingBetweenFields;

        EditorGUI.indentLevel += 1;

        if (!theGlobalOneProp.boolValue) DisplayProperty(ref position, globalProp);
        if (theGlobalOneProp.boolValue || !globalProp.boolValue)
        {
            DisplayProperty(ref position, colorProp);
            DisplayProperty(ref position, sizeProp);

            DisplayProperty(ref position, flagsProp);


            EditorGUI.indentLevel += 1;
            // if ((flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.UseAlpha) != 0)
            // {
            //     DisplayProperty(ref position, alphaProp);
            // }

            if (!theGlobalOneProp.boolValue)
            {
                if ((flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.ShowHidden) != 0)
                {
                    DisplayProperty(ref position, overrideHiddenProp);
                    if (overrideHiddenProp.boolValue)
                    {
                        DisplayProperty(ref position, hiddenMatProp);
                    }
                }
            }

            if ((flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.Precise) != 0)
            {
                DisplayProperty(ref position, passesProp);
            }

            EditorGUI.indentLevel -= 1;
        }

        EditorGUI.indentLevel -= 1;

        EditorGUI.EndProperty();
    }

    void DisplayProperty(ref Rect position, SerializedProperty prop)
    {
        EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, EditorGUI.GetPropertyHeight(prop)), prop);
        position.y += EditorGUIUtility.singleLineHeight + PaddingBetweenFields;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        theGlobalOneProp   = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.theGlobalOne));
        colorProp          = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.color));
        sizeProp           = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.outlineSize));
        flagsProp          = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.flags));
        globalProp         = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.useGlobalSettings));
        alphaProp          = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.alphaCutoff));
        passesProp         = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.shaderPasses));
        overrideHiddenProp = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.OverrideHiddenMaterial));
        hiddenMatProp      = property.FindPropertyRelative(nameof(OutlineFeature.OutlineSettings.HiddenMaterial));

        if (!theGlobalOneProp.boolValue && globalProp.boolValue) return (EditorGUIUtility.singleLineHeight + PaddingBetweenFields) * (2 + 1); // global checkbox + label

        var height = (EditorGUIUtility.singleLineHeight + PaddingBetweenFields) * (4 + 1); // 3 fields always visible + label

        //if ((flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.UseAlpha) != 0) height += EditorGUIUtility.singleLineHeight + PaddingBetweenFields;
        if ((flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.Precise) != 0) height  += EditorGUI.GetPropertyHeight(passesProp) + PaddingBetweenFields;
        if (!theGlobalOneProp.boolValue && (flagsProp.enumValueFlag & (int) OutlineFeature.OutlineFlags.ShowHidden) != 0)
        {
            height += EditorGUIUtility.singleLineHeight + PaddingBetweenFields;
            if (overrideHiddenProp.boolValue) height += EditorGUIUtility.singleLineHeight + PaddingBetweenFields;
        }

        return height;
    }
}

}