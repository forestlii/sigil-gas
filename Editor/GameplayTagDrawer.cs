// Copyright 2026 Likeon All Rights Reserved.
// 编辑器：GameplayTag 的属性绘制器 —— 层级下拉选择 + 搜索 + 一键新增标签。
// 让 Inspector 里不再手打字符串标签。

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // GameplayTag 内部序列化字段是 tagName
            var nameProp = property.FindPropertyRelative("tagName");
            if (nameProp == null) { EditorGUI.PropertyField(position, property, label); return; }

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            const float addW = 22f, gap = 2f;
            var ddRect = new Rect(position.x, position.y, position.width - addW - gap, position.height);
            var addRect = new Rect(ddRect.xMax + gap, position.y, addW, position.height);

            string cur = string.IsNullOrEmpty(nameProp.stringValue) ? "(None)" : nameProp.stringValue;

            // 下拉：选已注册标签
            if (EditorGUI.DropdownButton(ddRect, new GUIContent(cur, cur), FocusType.Keyboard))
            {
                var settings = GameplayTagSettingsUtil.GetOrCreate();
                var so = nameProp.serializedObject;
                var propPath = nameProp.propertyPath;
                var dd = new GameplayTagAdvancedDropdown(new AdvancedDropdownState(), settings.Tags, picked =>
                {
                    so.Update();
                    var p = so.FindProperty(propPath);
                    if (p != null) { p.stringValue = picked; so.ApplyModifiedProperties(); }
                });
                dd.Show(ddRect);
            }

            // ＋：新增标签
            if (GUI.Button(addRect, new GUIContent("+", "新增标签 Add new tag")))
            {
                var so = nameProp.serializedObject;
                var propPath = nameProp.propertyPath;
                PopupWindow.Show(addRect, new AddTagPopup(newTag =>
                {
                    var settings = GameplayTagSettingsUtil.GetOrCreate();
                    if (settings.AddTag(newTag)) GameplayTagSettingsUtil.Save(settings);
                    so.Update();
                    var p = so.FindProperty(propPath);
                    if (p != null) { p.stringValue = newTag.Trim(); so.ApplyModifiedProperties(); }
                }));
            }
        }
    }

    /// <summary>把点分标签构建成层级、可搜索的下拉。</summary>
    internal class GameplayTagAdvancedDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<string> _tags;
        private readonly Action<string> _onPick;
        private readonly Dictionary<int, string> _idToTag = new Dictionary<int, string>();

        public GameplayTagAdvancedDropdown(AdvancedDropdownState state, IReadOnlyList<string> tags, Action<string> onPick) : base(state)
        {
            _tags = tags;
            _onPick = onPick;
            minimumSize = new Vector2(260, 340);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Gameplay Tags");

            var none = new AdvancedDropdownItem("(None)");
            root.AddChild(none);
            _idToTag[none.id] = string.Empty;

            var nodes = new Dictionary<string, AdvancedDropdownItem>();
            if (_tags != null)
            {
                foreach (var tag in _tags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    var parts = tag.Split('.');
                    AdvancedDropdownItem parent = root;
                    string path = string.Empty;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        path = i == 0 ? parts[0] : path + "." + parts[i];
                        if (!nodes.TryGetValue(path, out var node))
                        {
                            node = new AdvancedDropdownItem(parts[i]);
                            nodes[path] = node;
                            parent.AddChild(node);
                            _idToTag[node.id] = path; // 中间节点也可选（=该前缀标签）
                        }
                        parent = node;
                    }
                }
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (_idToTag.TryGetValue(item.id, out var tag)) _onPick?.Invoke(tag);
        }
    }

    /// <summary>新增标签的小弹窗（文本框 + 回车/Add 确认）。</summary>
    internal class AddTagPopup : PopupWindowContent
    {
        private readonly Action<string> _onAdd;
        private string _text = string.Empty;
        private bool _focus = true;

        public AddTagPopup(Action<string> onAdd) { _onAdd = onAdd; }

        public override Vector2 GetWindowSize() => new Vector2(280, 64);

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label("新增 Gameplay Tag（如 State.Combat.Attacking）", EditorStyles.miniBoldLabel);

            GUI.SetNextControlName("AddTagField");
            _text = EditorGUILayout.TextField(_text);
            if (_focus) { EditorGUI.FocusTextInControl("AddTagField"); _focus = false; }

            bool enter = Event.current.type == EventType.KeyDown
                         && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add", GUILayout.Width(60)) || enter)
            {
                if (!string.IsNullOrWhiteSpace(_text))
                {
                    _onAdd?.Invoke(_text);
                    editorWindow.Close();
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
