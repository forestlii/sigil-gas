// Copyright 2026 Likeon All Rights Reserved.
// 把攻击伤害减去目标减伤后映射成 -Health。
// 这条是你源码里"真实伤害走 AS_Combat 执行计算"的路径（区别于直接 SetByCaller 到 IncomingDamage）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 伤害执行：最终伤害 = (来源 AS_Combat.Damage + SetByCaller 伤害) − 目标 AS_Combat.DamageNegation
    ///                  （格挡时再减 GuardDamageNegation），结果写入目标 AS_Health.IncomingDamage。
    /// IncomingDamage 再由 AS_Health 的 Meta 管线映射成 -Health。
    /// </summary>
    [CreateAssetMenu(fileName = "Exec_Damage", menuName = "Likeon/GAS/Damage Execution")]
    public class DamageExecutionCalculation : GameplayEffectExecutionCalculation
    {
        [Tooltip("SetByCaller 里代表附加伤害的标签（如 Data.Damage）")]
        public GameplayTag DamageSetByCallerTag = GameplayTag.RequestTag("Data.Damage");

        [Tooltip("目标拥有此标签时按格挡处理（额外减 GuardDamageNegation）")]
        public GameplayTag BlockingStateTag = GameplayTag.RequestTag("State.Blocking");

        public override void Execute(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC, List<GameplayExecutionOutput> outputs)
        {
            if (targetASC == null) return;

            // 基础伤害：来源 AS_Combat.Damage + SetByCaller
            float damage = 0f;
            var srcCombat = sourceASC != null ? sourceASC.GetAttributeSet<AS_Combat>() : null;
            if (srcCombat != null) damage += srcCombat.Damage.CurrentValue;
            damage += spec.GetSetByCallerMagnitude(DamageSetByCallerTag, 0f);

            // 减伤：目标 AS_Combat.DamageNegation（+ 格挡时 GuardDamageNegation）
            var tgtCombat = targetASC.GetAttributeSet<AS_Combat>();
            if (tgtCombat != null)
            {
                damage -= tgtCombat.DamageNegation.CurrentValue;
                if (BlockingStateTag.IsValid && targetASC.HasMatchingGameplayTag(BlockingStateTag))
                    damage -= tgtCombat.GuardDamageNegation.CurrentValue;
            }

            damage = Mathf.Max(0f, damage);
            if (damage <= 0f) return;

            // 写入目标 IncomingDamage（Meta），由 AS_Health 映射成 -Health
            var health = targetASC.GetAttributeSet<AS_Health>();
            if (health != null)
                outputs.Add(new GameplayExecutionOutput(health.IncomingDamageAttribute, damage));
        }
    }
}
