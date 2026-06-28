// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 传给 AttributeSet.PostGameplayEffectExecute 的结算信息。

namespace Likeon.GAS
{
    /// <summary>
    /// 一次属性结算的回调数据。AttributeSet 在 PreGameplayEffectExecute/PostGameplayEffectExecute
    /// 里读它，判断"是哪个属性、被改了多少、谁改的"——Meta Attribute 伤害管线就在这里读 IncomingDamage。
    /// </summary>
    public struct GameplayEffectModCallbackData
    {
        /// <summary>本次结算施加的效果规格。</summary>
        public GameplayEffectSpec Spec;
        /// <summary>被修改的属性。</summary>
        public GameplayAttribute Attribute;
        /// <summary>结算后写入的增量（已按运算符计算后的"等效加减量"）。</summary>
        public float EvaluatedMagnitude;
        /// <summary>效果作用的目标 ASC。</summary>
        public AbilitySystemComponent TargetASC;
        /// <summary>效果来源 ASC（可空）。</summary>
        public AbilitySystemComponent SourceASC => Spec?.Context?.SourceASC;
    }
}
