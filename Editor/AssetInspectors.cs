// Copyright 2026 Likeon All Rights Reserved.
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
            EditorGUILayout.HelpBox($"激活组: {ab.ActivationPolicy}    身份标签: {tagCount} 个", MessageType.None);
            if (tagCount == 0)
                EditorGUILayout.HelpBox("没有 AbilityTags：将无法用 TryActivateAbilitiesByTag / 标签关系来匹配本技能。", MessageType.Warning);

            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(AttackDefinition))]
    public class AttackDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var atk = (AttackDefinition)target;
            EditorGUILayout.HelpBox("Attack Definition — 命中目标后施加的效果与表现。", MessageType.None);

            bool hasContainerEffect = atk.TargetEffectContainer.TargetGameplayEffects != null
                                      && atk.TargetEffectContainer.TargetGameplayEffects.Count > 0;
            if (atk.TargetEffect == null && !hasContainerEffect)
                EditorGUILayout.HelpBox("既没有 TargetEffect 也没有效果容器：命中将不产生任何效果。", MessageType.Warning);

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
            int attrSets = set.GrantedAttributeSetTypes != null ? set.GrantedAttributeSetTypes.Count : 0;
            EditorGUILayout.HelpBox($"Ability Set — 技能 {abilities} · 效果 {effects} · 属性集 {attrSets}", MessageType.None);

            DrawDefaultInspector();
        }
    }
}
