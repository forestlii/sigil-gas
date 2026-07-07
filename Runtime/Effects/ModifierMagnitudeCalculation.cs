// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 单条 modifier 的自定义修改量计算（如"伤害 = 力量 × 1.5 + 等级 × 2"）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 自定义修改量计算基类（ScriptableObject 资产，可被多个 GameplayEffect 的 modifier 复用）。
    /// 对齐 UE UGameplayModMagnitudeCalculation：子类重写 <see cref="CalculateBaseMagnitude"/>
    /// 读取来源/目标属性 + spec 信息，算出这一条 modifier 的最终数值。
    ///
    /// 与 <see cref="GameplayEffectExecutionCalculation"/> 的分工：
    /// - MMC 只算「一条 modifier 的 magnitude」，返回单个 float，仍走标准 modifier 聚合管线
    ///   （能参与 Duration/Infinite 的当前值重算，可叠层放大）；
    /// - Execution 可一次改「多个属性」、产出多条 <see cref="GameplayExecutionOutput"/>，只在瞬时/周期结算时跑。
    /// 简单的属性联动公式用 MMC；带护甲/暴击/多属性联动的复杂结算用 Execution。
    /// </summary>
    public abstract class ModifierMagnitudeCalculation : ScriptableObject
    {
        /// <summary>
        /// 计算这一条 modifier 的修改量。
        /// </summary>
        /// <param name="spec">效果规格（含 Level / SetByCaller / Context 等）。</param>
        /// <param name="sourceASC">效果来源的 ASC（可能为 null，如无来源上下文的自施加初始化）。</param>
        /// <param name="targetASC">效果目标的 ASC（施加对象；通常非 null）。</param>
        /// <returns>算出的修改量，交给 modifier 的运算符（Add/Multiply/...）施加。</returns>
        public abstract float CalculateBaseMagnitude(
            GameplayEffectSpec spec,
            AbilitySystemComponent sourceASC,
            AbilitySystemComponent targetASC);
    }
}
