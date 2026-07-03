// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// GAS 运行时调试器窗口：Play Mode 下选中任意 GameObject（或其子物体），
// 若其父链上有 AbilitySystemComponent，即可实时查看属性 / 拥有标签 / 已授予技能 / 激活效果 / 事件日志。

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    public class GASDebuggerWindow : EditorWindow
    {
        private const double AttributeFlashSeconds = 0.8;

        private readonly GASDebuggerSession _session = new GASDebuggerSession();
        private bool _locked;
        private Vector2 _scroll;
        private Vector2 _logScroll;

        private bool _showAttributes = true;
        private bool _showTags = true;
        private bool _showAbilities = true;
        private bool _showEffects = true;
        private bool _showLog = true;

        // 复用的临时缓冲，避免每帧分配
        private readonly List<KeyValuePair<GameplayTag, int>> _tagCountsBuffer = new List<KeyValuePair<GameplayTag, int>>();
        private readonly StringBuilder _sb = new StringBuilder();

        [MenuItem("Likeon/GAS/GAS Debugger")]
        public static void Open()
        {
            var window = GetWindow<GASDebuggerWindow>("GAS Debugger");
            window.minSize = new Vector2(360f, 300f);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _session.Dispose();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // 退出 Play Mode 时目标即将销毁，主动断开订阅
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                _session.Dispose();
                _locked = false;
            }
        }

        private void OnInspectorUpdate() => Repaint(); // ~10Hz，Play Mode 下数据在变

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect a live AbilitySystemComponent.", MessageType.Info);
                return;
            }

            ResolveTarget();
            DrawToolbar();

            var asc = _session.Target;
            if (asc == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with an AbilitySystemComponent (searched on itself and its parents), or pick one from the dropdown above.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawAttributes(asc);
            DrawTags(asc);
            DrawAbilities(asc);
            DrawEffects(asc);
            DrawEventLog();
            EditorGUILayout.EndScrollView();
        }

        // ---- 目标解析与工具栏 ----

        private void ResolveTarget()
        {
            if (_locked && _session.Target != null) return;

            // 跟随 Hierarchy/Scene 选中；选中无 ASC 的对象时保持上一个目标（粘滞），避免误点清空窗口
            var go = Selection.activeGameObject;
            if (go == null) return;
            var asc = go.GetComponentInParent<AbilitySystemComponent>(true);
            if (asc != null) _session.SetTarget(asc);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var label = _session.Target != null ? _session.Target.gameObject.name : "(no target)";
            if (GUILayout.Button(label, EditorStyles.toolbarDropDown, GUILayout.MinWidth(140f)))
                ShowTargetPicker();

            GUILayout.FlexibleSpace();

            _locked = GUILayout.Toggle(_locked, "Lock", EditorStyles.toolbarButton, GUILayout.Width(44f));

            using (new EditorGUI.DisabledScope(_session.Target == null))
            {
                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                    EditorGUIUtility.PingObject(_session.Target.gameObject);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowTargetPicker()
        {
            var menu = new GenericMenu();
            var all = FindObjectsByType<AbilitySystemComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (all.Length == 0) menu.AddDisabledItem(new GUIContent("(no AbilitySystemComponent in scene)"));
            foreach (var asc in all)
            {
                var captured = asc;
                bool on = ReferenceEquals(asc, _session.Target);
                menu.AddItem(new GUIContent(asc.gameObject.name), on, () =>
                {
                    _session.SetTarget(captured);
                    Selection.activeGameObject = captured.gameObject;
                    EditorGUIUtility.PingObject(captured.gameObject);
                });
            }
            menu.ShowAsContext();
        }

        // ---- 面板 ----

        private void DrawAttributes(AbilitySystemComponent asc)
        {
            var sets = asc.GetAttributeSets();
            _showAttributes = EditorGUILayout.Foldout(_showAttributes, $"Attributes ({sets.Count} sets)", true);
            if (!_showAttributes) return;

            double now = _session.Clock();
            using (new EditorGUI.IndentLevelScope())
            {
                if (sets.Count == 0) EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                for (int i = 0; i < sets.Count; i++)
                {
                    var set = sets[i];
                    EditorGUILayout.LabelField(set.GetType().Name, EditorStyles.boldLabel);
                    foreach (var kv in set.Attributes)
                    {
                        var rowRect = EditorGUILayout.BeginHorizontal();

                        // 最近变更过的属性行闪黄（随时间淡出）
                        string key = set.GetType().FullName + "." + kv.Key;
                        if (_session.RecentAttributeChanges.TryGetValue(key, out double t))
                        {
                            float age = (float)(now - t);
                            if (age < AttributeFlashSeconds)
                                EditorGUI.DrawRect(rowRect, new Color(1f, 0.9f, 0.2f, 0.35f * (1f - age / (float)AttributeFlashSeconds)));
                        }

                        EditorGUILayout.LabelField(kv.Key, GUILayout.MinWidth(120f));
                        EditorGUILayout.LabelField($"Base {kv.Value.BaseValue:F2}", EditorStyles.miniLabel, GUILayout.Width(90f));

                        // Current 与 Base 不同（有临时修改器在生效）时着色提示
                        bool modified = !Mathf.Approximately(kv.Value.BaseValue, kv.Value.CurrentValue);
                        var prev = GUI.color;
                        if (modified) GUI.color = new Color(0.4f, 0.85f, 1f);
                        EditorGUILayout.LabelField($"Cur {kv.Value.CurrentValue:F2}", modified ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(90f));
                        GUI.color = prev;

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private void DrawTags(AbilitySystemComponent asc)
        {
            asc.GetOwnedGameplayTagCounts(_tagCountsBuffer);
            _showTags = EditorGUILayout.Foldout(_showTags, $"Owned Tags ({_tagCountsBuffer.Count})", true);
            if (!_showTags) return;

            _tagCountsBuffer.Sort((a, b) => string.CompareOrdinal(a.Key.TagName, b.Key.TagName));
            using (new EditorGUI.IndentLevelScope())
            {
                if (_tagCountsBuffer.Count == 0) EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                foreach (var kv in _tagCountsBuffer)
                    EditorGUILayout.LabelField(kv.Value > 1 ? $"{kv.Key.TagName}  ×{kv.Value}" : kv.Key.TagName);
            }
        }

        private void DrawAbilities(AbilitySystemComponent asc)
        {
            var abilities = asc.GetGrantedAbilities();
            _showAbilities = EditorGUILayout.Foldout(_showAbilities, $"Abilities ({abilities.Count})", true);
            if (!_showAbilities) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (abilities.Count == 0) EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                foreach (var spec in abilities)
                {
                    var ability = spec.Ability;
                    if (ability == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(ability.name, EditorStyles.boldLabel, GUILayout.MinWidth(130f));

                    if (spec.IsActive)
                    {
                        var prev = GUI.color;
                        GUI.color = new Color(0.4f, 1f, 0.4f);
                        GUILayout.Label("Active", EditorStyles.miniButton, GUILayout.Width(50f));
                        GUI.color = prev;
                    }
                    else if (asc.AreAbilityTagsBlocked(ability.GetAbilityTags()))
                    {
                        var prev = GUI.color;
                        GUI.color = new Color(1f, 0.55f, 0.4f);
                        GUILayout.Label("Blocked", EditorStyles.miniButton, GUILayout.Width(58f));
                        GUI.color = prev;
                    }

                    if (ability.ActivationGroup != EAbilityActivationGroup.Independent)
                        GUILayout.Label(ability.ActivationGroup.ToString(), EditorStyles.miniLabel, GUILayout.Width(130f));

                    DrawCooldown(asc, ability);
                    EditorGUILayout.EndHorizontal();

                    if (ability.AbilityTags.Count > 0)
                        EditorGUILayout.LabelField(JoinTags(ability.AbilityTags), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawCooldown(AbilitySystemComponent asc, GameplayAbility ability)
        {
            if (ability.CooldownEffect == null || ability.CooldownEffect.GrantedTags.Count == 0) return;
            if (!asc.GetCooldownRemainingForTags(ability.CooldownEffect.GrantedTags, out float remaining, out float duration) || remaining <= 0f)
                return;

            var rect = GUILayoutUtility.GetRect(90f, EditorGUIUtility.singleLineHeight, GUILayout.Width(90f));
            EditorGUI.ProgressBar(rect, duration > 0f ? remaining / duration : 0f, $"CD {remaining:F1}s");
        }

        private void DrawEffects(AbilitySystemComponent asc)
        {
            var effects = asc.GetActiveGameplayEffects();
            _showEffects = EditorGUILayout.Foldout(_showEffects, $"Active Effects ({effects.Count})", true);
            if (!_showEffects) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (effects.Count == 0) EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
                for (int i = 0; i < effects.Count; i++)
                {
                    var ae = effects[i];
                    var prev = GUI.color;
                    if (ae.Inhibited) GUI.color = new Color(1f, 1f, 1f, 0.45f); // 被 Ongoing 条件抑制的整行淡显

                    EditorGUILayout.BeginHorizontal();
                    _sb.Length = 0;
                    _sb.Append(ae.Def.name);
                    if (ae.StackCount > 1) _sb.Append("  ×").Append(ae.StackCount);
                    if (ae.Inhibited) _sb.Append("  (inhibited)");
                    EditorGUILayout.LabelField(_sb.ToString(), EditorStyles.boldLabel, GUILayout.MinWidth(150f));

                    if (ae.Def.IsPeriodic)
                        GUILayout.Label($"tick {ae.PeriodRemaining:F1}s", EditorStyles.miniLabel, GUILayout.Width(70f));

                    if (ae.Def.DurationType == EGameplayEffectDurationType.HasDuration && ae.Def.Duration > 0f)
                    {
                        var rect = GUILayoutUtility.GetRect(90f, EditorGUIUtility.singleLineHeight, GUILayout.Width(90f));
                        EditorGUI.ProgressBar(rect, ae.TimeRemaining / ae.Def.Duration, $"{ae.TimeRemaining:F1}s");
                    }
                    else
                    {
                        GUILayout.Label("∞", GUILayout.Width(20f));
                    }
                    EditorGUILayout.EndHorizontal();

                    if (ae.Def.GrantedTags.Count > 0)
                        EditorGUILayout.LabelField("grants: " + JoinTags(ae.Def.GrantedTags), EditorStyles.miniLabel);

                    GUI.color = prev;
                }
            }
        }

        private void DrawEventLog()
        {
            EditorGUILayout.BeginHorizontal();
            _showLog = EditorGUILayout.Foldout(_showLog, $"Event Log ({_session.Log.Count})", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                _session.Log.Clear();
            EditorGUILayout.EndHorizontal();
            if (!_showLog) return;

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(180f));
            // 新事件在最上面
            for (int i = _session.Log.Count - 1; i >= 0; i--)
            {
                var entry = _session.Log[i];
                EditorGUILayout.LabelField($"[{entry.Time:F2}] {entry.Text}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private string JoinTags(List<GameplayTag> tags)
        {
            _sb.Length = 0;
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0) _sb.Append("  ");
                _sb.Append(tags[i].TagName);
            }
            return _sb.ToString();
        }
    }
}
