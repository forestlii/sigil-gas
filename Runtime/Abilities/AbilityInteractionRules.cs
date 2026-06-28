// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 技能交互规则：把"技能之间的 block / cancel / 激活准入"数据化。
// 支持状态感知的条件规则——按角色当前标签查询，动态决定哪些规则此刻生效。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>一条技能交互规则。</summary>
    [Serializable]
    public struct AbilityTagRule
    {
        [Tooltip("这些规则适用的技能标签")]
        public GameplayTag AbilityTag;
        [Tooltip("本技能激活时，阻挡带这些标签的技能")]
        public List<GameplayTag> AbilityTagsToBlock;
        [Tooltip("本技能执行时，取消带这些标签的技能")]
        public List<GameplayTag> AbilityTagsToCancel;
        [Tooltip("激活本技能所需的角色标签")]
        public List<GameplayTag> ActivationRequiredTags;
        [Tooltip("角色拥有这些标签则阻止激活")]
        public List<GameplayTag> ActivationBlockedTags;
    }

    /// <summary>带状态查询条件的一组规则：满足查询时这组规则才生效。</summary>
    [Serializable]
    public struct ConditionalAbilityTagRules
    {
        [Tooltip("满足此查询（对角色当前标签）时，下面的规则才生效")]
        public GameplayTagQuery ActorTagQuery;
        public List<AbilityTagRule> Rules;
    }

    /// <summary>
    /// 技能交互规则资产。ASC 在激活技能时用它动态决定 block / cancel 与激活准入。
    /// 含状态感知的条件规则（按角色当前标签叠加）。
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityInteractionRules_New", menuName = "Likeon/GAS/Ability Interaction Rules")]
    public class AbilityInteractionRules : ScriptableObject
    {
        [Header("基础规则（始终生效）")]
        [SerializeField] private List<AbilityTagRule> baseRules = new List<AbilityTagRule>();

        [Header("条件规则（按角色当前标签查询生效）")]
        [SerializeField] private List<ConditionalAbilityTagRules> conditionalRules = new List<ConditionalAbilityTagRules>();

        /// <summary>收集本技能要 block / cancel 的标签（叠加满足查询的条件规则）。</summary>
        public void CollectBlockedAndCanceledTags(GameplayTagContainer actorTags, GameplayTagContainer abilityTags,
            GameplayTagContainer outBlock, GameplayTagContainer outCancel)
        {
            ForEachApplicable(actorTags, abilityTags, rule =>
            {
                AppendAll(rule.AbilityTagsToBlock, outBlock);
                AppendAll(rule.AbilityTagsToCancel, outCancel);
            });
        }

        /// <summary>收集激活本技能所需 / 被禁止的角色标签（状态感知）。这是按状态动态门控技能的核心。</summary>
        public void CollectActivationRequirements(GameplayTagContainer actorTags, GameplayTagContainer abilityTags,
            GameplayTagContainer outRequired, GameplayTagContainer outBlocked)
        {
            ForEachApplicable(actorTags, abilityTags, rule =>
            {
                AppendAll(rule.ActivationRequiredTags, outRequired);
                AppendAll(rule.ActivationBlockedTags, outBlocked);
            });
        }

        /// <summary>本技能是否被某个动作标签取消。</summary>
        public bool IsCanceledByActionTag(GameplayTagContainer abilityTags, GameplayTag actionTag)
        {
            foreach (var rule in baseRules)
                if (abilityTags.HasTag(rule.AbilityTag) && Contains(rule.AbilityTagsToCancel, actionTag))
                    return true;
            return false;
        }

        // 遍历适用于该 abilityTags 的规则：基础 + 满足查询的条件规则。
        private void ForEachApplicable(GameplayTagContainer actorTags, GameplayTagContainer abilityTags, Action<AbilityTagRule> action)
        {
            foreach (var rule in baseRules)
                if (abilityTags.HasTag(rule.AbilityTag)) action(rule);

            foreach (var group in conditionalRules)
            {
                if (group.ActorTagQuery != null && !group.ActorTagQuery.Matches(actorTags)) continue;
                if (group.Rules == null) continue;
                foreach (var rule in group.Rules)
                    if (abilityTags.HasTag(rule.AbilityTag)) action(rule);
            }
        }

        private static void AppendAll(List<GameplayTag> src, GameplayTagContainer dst)
        {
            if (src == null || dst == null) return;
            foreach (var t in src) dst.AddTag(t);
        }

        private static bool Contains(List<GameplayTag> list, GameplayTag tag)
        {
            if (list == null) return false;
            foreach (var t in list) if (t.MatchesTagExact(tag)) return true;
            return false;
        }
    }
}
