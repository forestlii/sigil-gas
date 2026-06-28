// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 描述"对属性/标签做什么"的数据资产。
// Unity 里用 ScriptableObject 资产，更符合 Unity 数据驱动习惯。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 游戏效果资产。GAS 中改属性、挂标签的唯一正规手段。
    /// 通过 <see cref="AbilitySystemComponent.ApplyGameplayEffectToSelf"/> 等施加。
    /// </summary>
    [CreateAssetMenu(fileName = "GE_New", menuName = "Likeon/GAS/Gameplay Effect")]
    public class GameplayEffect : ScriptableObject
    {
        [Header("持续策略 Duration")]
        [Tooltip("Instant=改基础值; HasDuration=限时改当前值; Infinite=持续改当前值")]
        public EGameplayEffectDurationType DurationType = EGameplayEffectDurationType.Instant;
        [Tooltip("HasDuration 时的时长（秒）")]
        public float Duration = 0f;
        [Tooltip(">0 时为周期性效果，每隔该秒数结算一次（按 Instant 语义改基础值）")]
        public float Period = 0f;

        [Header("叠层 Stacking（仅 Duration/Infinite 有效）")]
        [Tooltip("None=每次施加各算各的独立实例；AggregateByTarget=按目标合并成带层数的一个实例；AggregateBySource=按来源各合并一个")]
        public EGameplayEffectStackingType StackingType = EGameplayEffectStackingType.None;
        [Tooltip("最大层数（>0 生效；0 或负数=无上限）")]
        public int StackLimitCount = 0;
        [Tooltip("再次施加时是否刷新时长")]
        public EGameplayEffectStackingDurationRefreshPolicy StackDurationRefreshPolicy = EGameplayEffectStackingDurationRefreshPolicy.RefreshOnSuccessfulApplication;
        [Tooltip("再次施加时是否重置周期计时")]
        public EGameplayEffectStackingPeriodResetPolicy StackPeriodResetPolicy = EGameplayEffectStackingPeriodResetPolicy.ResetOnSuccessfulApplication;
        [Tooltip("到期时：整组清空 / 掉一层并刷新 / 仅刷新")]
        public EGameplayEffectStackingExpirationPolicy StackExpirationPolicy = EGameplayEffectStackingExpirationPolicy.ClearEntireStack;

        [Header("属性修改 Modifiers")]
        public List<GameplayModifierInfo> Modifiers = new List<GameplayModifierInfo>();

        [Header("自定义执行 Executions（如带防御计算的伤害）")]
        [Tooltip("，做复杂结算")]
        public List<GameplayEffectExecutionCalculation> Executions = new List<GameplayEffectExecutionCalculation>();

        [Header("授予标签 Granted Tags（仅 Duration/Infinite 有效）")]
        [Tooltip("效果存在期间挂在目标身上的标签的 GrantedTags")]
        public List<GameplayTag> GrantedTags = new List<GameplayTag>();

        [Header("本效果自身的标签")]
        [Tooltip("用于被其它效果按标签移除/查询区分中的资产标签")]
        public List<GameplayTag> AssetTags = new List<GameplayTag>();

        [Header("施加条件 Application Tag Requirements")]
        [Tooltip("目标必须拥有这些标签才能施加")]
        public List<GameplayTag> ApplicationRequiredTags = new List<GameplayTag>();
        [Tooltip("目标拥有任一这些标签则不能施加")]
        public List<GameplayTag> ApplicationBlockedTags = new List<GameplayTag>();

        [Header("持续条件 Ongoing Tag Requirements（Duration/Infinite）")]
        [Tooltip("持续期间目标必须拥有这些标签，否则效果被抑制")]
        public List<GameplayTag> OngoingRequiredTags = new List<GameplayTag>();

        [Header("表现 GameplayCues")]
        [Tooltip("施加效果时触发的 cue 标签：Instant→Executed，Duration/Infinite→施加时 OnActive、移除时 Removed")]
        public List<GameplayTag> GameplayCues = new List<GameplayTag>();

        [Header("移除其它效果 Removal")]
        [Tooltip("施加本效果时，移除目标身上带这些资产标签的其它效果")]
        public List<GameplayTag> RemoveEffectsWithTags = new List<GameplayTag>();

        public bool IsInstant => DurationType == EGameplayEffectDurationType.Instant;
        public bool IsPeriodic => Period > 0f;
        public bool IsStackable => StackingType != EGameplayEffectStackingType.None;
    }
}
