// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 技能事件触发配置：授予后由 ASC 自动监听，匹配时自动激活。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能触发源（对齐 UE EGameplayAbilityTriggerSource）。
    /// </summary>
    public enum EGameplayAbilityTriggerSource
    {
        /// <summary>收到与 TriggerTag 匹配的 GameplayEvent 时激活（事件数据作为 triggerData 传入技能）。</summary>
        GameplayEvent,
        /// <summary>拥有的标签出现（计数 0→有）时激活一次。</summary>
        OwnedTagAdded,
        /// <summary>标签出现时激活；标签消失（计数→0）时若技能仍在激活则取消（把技能生命周期绑到标签存在期间）。</summary>
        OwnedTagPresent,
    }

    /// <summary>
    /// 单条技能触发配置（对齐 UE FAbilityTriggerData）。
    /// 挂在 <see cref="GameplayAbility.AbilityTriggers"/> 上，授予后由 ASC 自动响应。
    /// </summary>
    [Serializable]
    public class AbilityTrigger
    {
        [Tooltip("要监听的标签：GameplayEvent 源匹配事件 tag（层级匹配），OwnedTag 源匹配拥有的状态 tag（精确匹配）")]
        public GameplayTag TriggerTag;

        [Tooltip("触发源类型")]
        public EGameplayAbilityTriggerSource TriggerSource = EGameplayAbilityTriggerSource.GameplayEvent;

        /// <summary>触发标签是否有效。</summary>
        public bool IsValid => TriggerTag.IsValid;
    }
}
