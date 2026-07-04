// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器工具：[SerializeReference] 列表的通用编辑 GUI —— 按具体子类型下拉添加 + 删除 + 展开编辑。
// Unity 默认的 managed-reference 类型选择器不直观（尤其子类无可序列化字段时看着"点不动"），
// 故用显式的"+ 添加"下拉 + 类型短名折叠统一处理。AbilityLoadout / InputControlSetup 共用。

using System;
using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    public static class SerializeReferenceListGUI
    {
        /// <summary>
        /// 绘制一个 [SerializeReference] 列表。
        /// </summary>
        /// <param name="serializedObject">宿主 SerializedObject（Add/Remove 回调里用）。</param>
        /// <param name="list">目标列表属性（元素为 managed reference）。</param>
        /// <param name="baseType">元素基类型（枚举其非抽象子类供选择）。</param>
        /// <param name="title">分组标题。</param>
        /// <param name="addLabel">添加按钮文案（留空用 "+ Add {baseType.Name}"）。</param>
        /// <param name="emptyElementHint">元素展开却没有可编辑字段时的提示（如属性集）；留空则不提示。</param>
        public static void Draw(SerializedObject serializedObject, SerializedProperty list, Type baseType,
            string title, string addLabel = null, string emptyElementHint = null)
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
                        bool drewAny = false;
                        var it = el.Copy();
                        var end = it.GetEndProperty();
                        if (it.NextVisible(true))
                        {
                            while (!SerializedProperty.EqualContents(it, end))
                            {
                                EditorGUILayout.PropertyField(it, true);
                                drewAny = true;
                                if (!it.NextVisible(false)) break;
                            }
                        }
                        if (!drewAny && !string.IsNullOrEmpty(emptyElementHint))
                            EditorGUILayout.HelpBox(emptyElementHint, MessageType.Info);
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
            var label = string.IsNullOrEmpty(addLabel) ? $"+ Add {baseType.Name}" : addLabel;
            if (EditorGUILayout.DropdownButton(new GUIContent(label), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (var t in TypeCache.GetTypesDerivedFrom(baseType))
                {
                    if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
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
