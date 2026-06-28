// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：Likeon > GAS > Gameplay Tags —— 独立编辑器窗口，集中管理 Gameplay Tag 注册表。
// 取代原先挂在 Project Settings 下的入口，把插件的编辑器功能统一收进顶部 Likeon 菜单。

using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    public class GameplayTagsWindow : EditorWindow
    {
        private string _newTag = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Likeon/GAS/Gameplay Tags", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<GameplayTagsWindow>(false, "Gameplay Tags");
            window.minSize = new Vector2(320, 360);
            window.Show();
        }

        private void OnGUI()
        {
            var settings = GameplayTagSettingsUtil.GetOrCreate();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"已注册 {settings.Count} 个 Gameplay Tag", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这些标签会出现在所有 GameplayTag 字段的下拉选择器里。", MessageType.Info);
            EditorGUILayout.Space();

            // 新增
            using (new EditorGUILayout.HorizontalScope())
            {
                _newTag = EditorGUILayout.TextField("新增标签", _newTag);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newTag)))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(70)))
                    {
                        if (settings.AddTag(_newTag)) { GameplayTagSettingsUtil.Save(settings); _newTag = string.Empty; GUI.FocusControl(null); }
                    }
                }
            }

            // 扫描工程补标签（等同 Likeon/GAS/Scan Project for Gameplay Tags）
            if (GUILayout.Button("扫描工程补标签 Scan Project for Gameplay Tags"))
                GameplayTagScanner.Scan();

            EditorGUILayout.Space();

            // 列表（带删除）
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string toRemove = null;
            foreach (var tag in settings.Tags)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(tag);
                    if (GUILayout.Button("－", GUILayout.Width(28))) toRemove = tag;
                }
            }
            EditorGUILayout.EndScrollView();

            if (toRemove != null && settings.RemoveTag(toRemove))
                GameplayTagSettingsUtil.Save(settings);
        }
    }
}
