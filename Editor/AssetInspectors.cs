// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：关键资产的增强 Inspector（摘要 + 配置校验提示 + 默认绘制）。

using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    [CustomEditor(typeof(GameplayEffect))]
    public class GameplayEffectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ge = (GameplayEffect)target;
            EditorGUILayout.HelpBox("Gameplay Effect — 改属性 / 挂标签 的数据资产。", MessageType.None);

            if (ge.DurationType == EGameplayEffectDurationType.Instant)
            {
                if (ge.GrantedTags != null && ge.GrantedTags.Count > 0)
                    EditorGUILayout.HelpBox("Instant 效果的 GrantedTags 不会持续生效（瞬时无存活期）。", MessageType.Warning);
            }
            else if (ge.DurationType == EGameplayEffectDurationType.HasDuration && ge.Duration <= 0f)
            {
                EditorGUILayout.HelpBox("HasDuration 但 Duration ≤ 0：效果会立刻结束。", MessageType.Warning);
            }
            if (ge.Period < 0f)
                EditorGUILayout.HelpBox("Period 为负值无意义（>0 才是周期效果）。", MessageType.Warning);

            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(GameplayAbility), true)]
    public class GameplayAbilityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ab = (GameplayAbility)target;
            int tagCount = ab.AbilityTags != null ? ab.AbilityTags.Count : 0;
            EditorGUILayout.HelpBox($"激活组: {ab.ActivationGroup}    身份标签: {tagCount} 个", MessageType.None);
            if (tagCount == 0)
                EditorGUILayout.HelpBox("没有 AbilityTags：将无法用 TryActivateAbilitiesByTag / 标签关系来匹配本技能。", MessageType.Warning);

            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(AbilityLoadout))]
    public class AbilityLoadoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var set = (AbilityLoadout)target;
            int abilities = set.GrantedAbilities != null ? set.GrantedAbilities.Count : 0;
            int effects = set.GrantedEffects != null ? set.GrantedEffects.Count : 0;
            int attrSets = set.GrantedAttributeSets != null ? set.GrantedAttributeSets.Count : 0;
            EditorGUILayout.HelpBox($"Ability Loadout — 技能 {abilities} · 效果 {effects} · 属性集 {attrSets}", MessageType.None);

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GrantedAbilities"), true);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("GrantedEffects"), true);
            EditorGUILayout.Space();

            // GrantedAttributeSets 是 [SerializeReference] List<AttributeSet>：用显式的按子类型下拉添加，
            // 而非 Unity 默认那套（子类字段全 readonly 时看着"点不动无法编辑"）。
            SerializeReferenceListGUI.Draw(
                serializedObject,
                serializedObject.FindProperty("GrantedAttributeSets"),
                typeof(AttributeSet),
                "属性集（选具体子类，如 AS_Health）",
                "+ 添加属性集 (Attribute Set)",
                "该属性集在此无可配字段——起始数值由类定义决定；要按装载自定义初始值，请用 GrantedEffects 里的初始化效果（Instant/Infinite GE + SetByCaller / 曲线表）。");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
