// Copyright (c) 2026 Likeon. Licensed under the MIT License.

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>效果持续策略。</summary>
    public enum EGameplayEffectDurationType
    {
        /// <summary>瞬时：立即改 BaseValue（如一次伤害）。</summary>
        Instant,
        /// <summary>有限时长：改 CurrentValue，到期移除回退（如 5 秒减速）。</summary>
        HasDuration,
        /// <summary>无限：改 CurrentValue，直到手动移除（如装备加成）。</summary>
        Infinite
    }

    /// <summary>
    /// 效果叠层方式。决定同一个效果反复施加时如何合并。
    /// None=不合并（每次施加各算各的独立实例）；
    /// AggregateByTarget=按目标合并成一个带层数的实例（不分来源）；
    /// AggregateBySource=按来源各合并一个（同一来源的多次施加合并，不同来源分开）。
    /// </summary>
    public enum EGameplayEffectStackingType
    {
        None,
        AggregateByTarget,
        AggregateBySource
    }

    /// <summary>叠层时的时长刷新策略：再次施加是否把倒计时刷回满。</summary>
    public enum EGameplayEffectStackingDurationRefreshPolicy
    {
        /// <summary>每次成功施加都刷新时长（DoT 续期常用）。</summary>
        RefreshOnSuccessfulApplication,
        /// <summary>从不刷新（按首次施加的时长到期）。</summary>
        NeverRefresh
    }

    /// <summary>叠层时的周期重置策略：再次施加是否把周期结算计时归零。</summary>
    public enum EGameplayEffectStackingPeriodResetPolicy
    {
        /// <summary>每次成功施加都重置周期计时。</summary>
        ResetOnSuccessfulApplication,
        /// <summary>从不重置（周期照常推进）。</summary>
        NeverReset
    }

    /// <summary>叠层效果到期时的处理策略。</summary>
    public enum EGameplayEffectStackingExpirationPolicy
    {
        /// <summary>整组清空（一次性移除所有层）。</summary>
        ClearEntireStack,
        /// <summary>掉一层并刷新时长（逐层衰减，常用于 DoT）。</summary>
        RemoveSingleStackAndRefreshDuration,
        /// <summary>仅刷新时长（不掉层，需显式移除）。</summary>
        RefreshDuration
    }

    /// <summary>属性修改运算符。</summary>
    public enum EAttributeModifierOp
    {
        Add,
        Multiply,
        Divide,
        Override
    }

    /// <summary>
    /// 修改量来源。
    /// 固定值（可按等级缩放）、SetByCaller（运行时由施法者用标签传入）、曲线表按等级查值，
    /// 或 CustomCalculationClass（自定义 MMC 资产，支持"伤害 = 力量 × 1.5"这类属性联动公式）。
    /// </summary>
    [Serializable]
    public struct GameplayModifierMagnitude
    {
        public enum MagnitudeType { ScalableFloat, SetByCaller, CurveTableBased, CustomCalculationClass }

        [SerializeField] private MagnitudeType type;
        [Tooltip("ScalableFloat 模式：基础值")]
        [SerializeField] private float baseValue;
        [Tooltip("ScalableFloat 模式：每级增量，最终 = base + perLevel*(level-1)")]
        [SerializeField] private float perLevel;
        [Tooltip("SetByCaller 模式：运行时按此标签取值")]
        [SerializeField] private GameplayTag setByCallerTag;
        [Tooltip("CurveTableBased 模式：曲线表资产（按等级查值）")]
        [SerializeField] private CurveTable curveTable;
        [Tooltip("CurveTableBased 模式：曲线表里的行名")]
        [SerializeField] private string curveRowName;
        [Tooltip("CurveTableBased 模式：系数。最终 = 系数 × 曲线在该 level 的值（系数通常填 1）")]
        [SerializeField] private float coefficient;
        [Tooltip("CustomCalculationClass 模式：自定义 MMC 资产（读源/目标属性算 magnitude）")]
        [SerializeField] private ModifierMagnitudeCalculation customCalculation;

        public MagnitudeType Type => type;
        public GameplayTag SetByCallerTag => setByCallerTag;
        public ModifierMagnitudeCalculation CustomCalculation => customCalculation;

        /// <summary>
        /// 计算最终修改量。SetByCaller 从 spec 取；CurveTableBased 按 spec.Level 查曲线表；
        /// CustomCalculationClass 调 MMC 资产（用 source/target ASC 读属性算公式）。
        /// </summary>
        /// <param name="spec">效果规格（提供 Level / SetByCaller / Context）。</param>
        /// <param name="sourceASC">效果来源 ASC（供 MMC 读源属性；非 MMC 分支忽略）。</param>
        /// <param name="targetASC">效果目标 ASC（供 MMC 读目标属性；非 MMC 分支忽略）。</param>
        public float Evaluate(GameplayEffectSpec spec, AbilitySystemComponent sourceASC = null, AbilitySystemComponent targetASC = null)
        {
            int level = spec?.Level ?? 1;
            switch (type)
            {
                case MagnitudeType.SetByCaller:
                    return spec != null ? spec.GetSetByCallerMagnitude(setByCallerTag, 0f) : 0f;
                case MagnitudeType.CurveTableBased:
                    return curveTable != null ? coefficient * curveTable.Evaluate(curveRowName, level) : 0f;
                case MagnitudeType.CustomCalculationClass:
                    return customCalculation != null ? customCalculation.CalculateBaseMagnitude(spec, sourceASC, targetASC) : 0f;
                case MagnitudeType.ScalableFloat:
                default:
                    return baseValue + perLevel * (level - 1);
            }
        }

        public static GameplayModifierMagnitude ScalableFloat(float baseVal, float perLevel = 0f)
            => new GameplayModifierMagnitude { type = MagnitudeType.ScalableFloat, baseValue = baseVal, perLevel = perLevel };

        public static GameplayModifierMagnitude SetByCaller(GameplayTag tag)
            => new GameplayModifierMagnitude { type = MagnitudeType.SetByCaller, setByCallerTag = tag };

        public static GameplayModifierMagnitude CurveTableBased(CurveTable table, string rowName, float coefficient = 1f)
            => new GameplayModifierMagnitude { type = MagnitudeType.CurveTableBased, curveTable = table, curveRowName = rowName, coefficient = coefficient };

        public static GameplayModifierMagnitude Custom(ModifierMagnitudeCalculation calculation)
            => new GameplayModifierMagnitude { type = MagnitudeType.CustomCalculationClass, customCalculation = calculation };
    }

    /// <summary>单条属性修改。</summary>
    [Serializable]
    public struct GameplayModifierInfo
    {
        [Tooltip("要修改的属性")]
        public GameplayAttribute Attribute;
        [Tooltip("运算符")]
        public EAttributeModifierOp Operation;
        [Tooltip("修改量")]
        public GameplayModifierMagnitude Magnitude;
    }
}
