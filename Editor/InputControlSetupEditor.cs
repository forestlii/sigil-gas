// Copyright 2026 Likeon All Rights Reserved.
// 编辑器：InputControlSetup —— [SerializeReference] 的检查器/处理器列表，按具体子类型下拉添加 + 删除 + 展开编辑。

using System;
using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    [CustomEditor(typeof(InputControlSetup))]
    public class InputControlSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("输入控制集：门控检查器（全部通过才放行）+ 多态分发处理器。", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("inputProcessorExecutionType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableInputBuffer"));
            EditorGUILayout.Space();

            DrawReferenceList(serializedObject.FindProperty("inputCheckers"), typeof(InputChecker), "门控检查器 (Checkers)");
            EditorGUILayout.Space();
            DrawReferenceList(serializedObject.FindProperty("inputProcessors"), typeof(InputProcessor), "处理器 (Processors)");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceList(SerializedProperty list, Type baseType, string title)
        {
            if (list == null) return;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            int removeIdx = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                string typeName = el.managedReferenceFullTypename;
                string shortName = string.IsNullOrEmpty(typeName) ? "(null)" : typeName.Substring(typeName.LastIndexOf('.') + 1);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        el.isExpanded = EditorGUILayout.Foldout(el.isExpanded, shortName, true);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("✕", GUILayout.Width(24))) removeIdx = i;
                    }
                    if (el.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        var it = el.Copy();
                        var end = it.GetEndProperty();
                        if (it.NextVisible(true))
                        {
                            while (!SerializedProperty.EqualContents(it, end))
                            {
                                EditorGUILayout.PropertyField(it, true);
                                if (!it.NextVisible(false)) break;
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            if (removeIdx >= 0)
            {
                list.DeleteArrayElementAtIndex(removeIdx);
                return;
            }

            // 按子类型下拉添加
            if (EditorGUILayout.DropdownButton(new GUIContent($"+ Add {baseType.Name}"), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (var t in TypeCache.GetTypesDerivedFrom(baseType))
                {
                    if (t.IsAbstract) continue;
                    var captured = t;
                    menu.AddItem(new GUIContent(t.Name), false, () =>
                    {
                        serializedObject.Update();
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(captured);
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("（无可用子类型）"));
                menu.ShowAsContext();
            }
        }
    }
}
