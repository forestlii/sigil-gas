// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 能力动作库：把若干 AbilityActionSet 按"能力标签"汇集成一个资产，
// 对齐 UE 能力动作集设置——给一个能力标签 + 施法者/目标状态，选出该技能此刻该播的动作。
// （此前 AbilityActionSet 只有数据壳、无容器、无消费者；此库 + CombatSystemComponent.QueryAbilityActions 把它接入。）

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 能力动作库资产：按能力标签持有多个 <see cref="AbilityActionSet"/>。
    /// <see cref="SelectBestAbilityActions"/> 先按能力标签选中对应 set，再按施法者/目标状态选出动作组。
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityActionLibrary_New", menuName = "Sigil/Combat/Ability Action Library")]
    public class AbilityActionLibrary : ScriptableObject
    {
        [Tooltip("按能力标签组织的动作集（每个 AbilityTag 一组，可含 Layered 条件分支）")]
        public List<AbilityActionSet> ActionSets = new List<AbilityActionSet>();

        /// <summary>
        /// 选出最匹配的动作：先按 <paramref name="abilityTags"/> 命中某个 set 的 AbilityTag，
        /// 再让该 set 按施法者/目标标签选出动作组。命中且非空则写入 <paramref name="outActions"/> 返回 true。
        /// 对齐 UE SelectBestAbilityActions。
        /// </summary>
        public bool SelectBestAbilityActions(GameplayTagContainer abilityTags,
            GameplayTagContainer sourceTags, GameplayTagContainer targetTags, List<AbilityAction> outActions)
        {
            if (outActions == null) return false;
            outActions.Clear();
            if (abilityTags == null || abilityTags.IsEmpty) return false;

            foreach (var set in ActionSets)
            {
                if (set == null || !set.AbilityTag.IsValid) continue;
                if (!abilityTags.HasTag(set.AbilityTag)) continue; // 按能力标签命中该 set（层级匹配）

                var actions = set.SelectActions(sourceTags, targetTags);
                if (actions != null && actions.Count > 0)
                {
                    outActions.AddRange(actions);
                    return true;
                }
            }
            return false;
        }
    }
}
