// Copyright 2026 Likeon All Rights Reserved.

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
    /// 固定值（可按等级缩放）或 SetByCaller（运行时由施法者用标签传入）。
    /// </summary>
    [Serializable]
    public struct GameplayModifierMagnitude
    {
        public enum MagnitudeType { ScalableFloat, SetByCaller }

        [SerializeField] private MagnitudeType type;
        [Tooltip("ScalableFloat 模式：基础值")]
        [SerializeField] private float baseValue;
        [Tooltip("ScalableFloat 模式：每级增量，最终 = base + perLevel*(level-1)")]
        [SerializeField] private float perLevel;
        [Tooltip("SetByCaller 模式：运行时按此标签取值")]
        [SerializeField] private GameplayTag setByCallerTag;

        public MagnitudeType Type => type;
        public GameplayTag SetByCallerTag => setByCallerTag;

        /// <summary>计算最终修改量。SetByCaller 模式从 spec 里取。</summary>
        public float Evaluate(GameplayEffectSpec spec)
        {
            switch (type)
            {
                case MagnitudeType.SetByCaller:
                    return spec != null ? spec.GetSetByCallerMagnitude(setByCallerTag, 0f) : 0f;
                case MagnitudeType.ScalableFloat:
                default:
                    int level = spec?.Level ?? 1;
                    return baseValue + perLevel * (level - 1);
            }
        }

        public static GameplayModifierMagnitude ScalableFloat(float baseVal, float perLevel = 0f)
            => new GameplayModifierMagnitude { type = MagnitudeType.ScalableFloat, baseValue = baseVal, perLevel = perLevel };

        public static GameplayModifierMagnitude SetByCaller(GameplayTag tag)
            => new GameplayModifierMagnitude { type = MagnitudeType.SetByCaller, setByCallerTag = tag };
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
