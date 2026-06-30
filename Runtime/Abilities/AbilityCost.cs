// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 模块化技能消耗：可插拔的非属性消耗（弹药、充能次数等），对齐 UE AdditionalCosts。
// 一个技能可挂多个 AbilityCost；激活前 CheckCost 全部要能支付，激活时 ApplyCost 扣除
//（标了 OnlyApplyCostOnHit 的留到命中时由技能调 GameplayAbility.ApplyOnHitCosts 扣）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能消耗基类（ScriptableObject 模板，子类实现具体消耗：弹药/充能/自定义资源）。
    /// 属性类消耗（扣 Stamina/Mana）仍用 GameplayAbility.CostEffect；这里覆盖"非属性 / 自定义"消耗。
    /// </summary>
    public abstract class AbilityCost : ScriptableObject
    {
        [Tooltip("仅在技能成功命中时才扣（对齐 UE 仅命中才扣的开关）。" +
                 "false=激活即扣；true=由技能逻辑确认命中后调 ApplyOnHitCosts 扣。")]
        public bool OnlyApplyCostOnHit = false;

        /// <summary>能否支付本消耗（如弹药是否充足）。返回 false 则技能无法激活。</summary>
        public abstract bool CheckCost(GameplayAbility ability);

        /// <summary>扣除本消耗（如弹药 -1）。由 ApplyCost / ApplyOnHitCosts 调用。</summary>
        public abstract void ApplyCost(GameplayAbility ability);
    }
}
