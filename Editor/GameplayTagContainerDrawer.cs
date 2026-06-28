// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：GameplayTagContainer 多选绘制器 —— 列出已含标签(可删) + 下拉添加(去重)。

using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public class GameplayTagContainerDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            var tags = property.FindPropertyRelative("tags");
            int n = tags != null ? tags.arraySize : 0;
            return line + 4 + (n + 1) * (line + Pad); // 标题 + 每行 + 添加按钮
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var tagsProp = property.FindPropertyRelative("tags");
            if (tagsProp == null) { EditorGUI.PropertyField(position, property, label); return; }

            float line = EditorGUIUtility.singleLineHeight;
            var headerRect = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded,
                $"{label.text}  ({tagsProp.arraySize})", true);
            if (!property.isExpanded) return;

            float y = headerRect.yMax + 4;
            int removeIdx = -1;

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var nameProp = tagsProp.GetArrayElementAtIndex(i).FindPropertyRelative("tagName");
                var rowRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, line));
                var lblRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 24, line);
                var delRect = new Rect(lblRect.xMax + 2, rowRect.y, 22, line);

                EditorGUI.LabelField(lblRect, string.IsNullOrEmpty(nameProp.stringValue) ? "(None)" : nameProp.stringValue);
                if (GUI.Button(delRect, new GUIContent("-", "移除"))) removeIdx = i;
                y += line + Pad;
            }

            // 添加
            var addRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, line));
            if (EditorGUI.DropdownButton(addRect, new GUIContent("+ Add Tag"), FocusType.Keyboard))
            {
                var settings = GameplayTagSettingsUtil.GetOrCreate();
                var so = property.serializedObject;
                var path = tagsProp.propertyPath;
                var dd = new GameplayTagAdvancedDropdown(new AdvancedDropdownState(), settings.Tags, picked =>
                {
                    if (string.IsNullOrEmpty(picked)) return;
                    so.Update();
                    var arr = so.FindProperty(path);
                    for (int i = 0; i < arr.arraySize; i++)
                        if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("tagName").stringValue == picked) return; // 去重
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).FindPropertyRelative("tagName").stringValue = picked;
                    so.ApplyModifiedProperties();
                });
                dd.Show(addRect);
            }

            if (removeIdx >= 0)
            {
                tagsProp.DeleteArrayElementAtIndex(removeIdx);
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
