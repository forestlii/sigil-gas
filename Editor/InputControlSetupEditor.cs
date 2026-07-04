// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：InputControlSetup —— [SerializeReference] 的检查器/处理器列表，按具体子类型下拉添加 + 删除 + 展开编辑。

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

            // [SerializeReference] 列表：按子类型下拉添加（见 SerializeReferenceListGUI）
            SerializeReferenceListGUI.Draw(serializedObject, serializedObject.FindProperty("inputCheckers"), typeof(InputChecker), "门控检查器 (Checkers)");
            EditorGUILayout.Space();
            SerializeReferenceListGUI.Draw(serializedObject, serializedObject.FindProperty("inputProcessors"), typeof(InputProcessor), "处理器 (Processors)");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
