// Copyright 2026 Likeon All Rights Reserved.
// 复杂结算（如带护甲/暴击的伤害公式）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>一次自定义执行产出的"对某属性的增量"。</summary>
    public struct GameplayExecutionOutput
    {
        public GameplayAttribute Attribute;
        public EAttributeModifierOp Operation;
        public float Magnitude;

        public GameplayExecutionOutput(GameplayAttribute attr, float magnitude, EAttributeModifierOp op = EAttributeModifierOp.Add)
        {
            Attribute = attr;
            Operation = op;
            Magnitude = magnitude;
        }
    }

    /// <summary>
    /// 自定义执行计算基类（ScriptableObject 资产，可被多个 GameplayEffect 复用）。
    /// 子类重写 <see cref="Execute"/> 读取来源/目标属性，算出最终要施加的增量。
    ///::Execute_Implementation。
    /// </summary>
    public abstract class GameplayEffectExecutionCalculation : ScriptableObject
    {
        /// <summary>
        /// 执行结算。从 source/target ASC 抓取属性，产出若干 <see cref="GameplayExecutionOutput"/>。
        /// </summary>
        public abstract void Execute(
            GameplayEffectSpec spec,
            AbilitySystemComponent sourceASC,
            AbilitySystemComponent targetASC,
            List<GameplayExecutionOutput> outputs);
    }
}
