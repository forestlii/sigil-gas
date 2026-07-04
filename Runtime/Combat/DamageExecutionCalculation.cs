// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 把攻击伤害减去目标减伤后映射成 -Health 的执行计算路径（区别于直接 SetByCaller 到 IncomingDamage）。
// 涉及的属性全按名解析（默认 Damage / DamageNegation / GuardDamageNegation / IncomingDamage）——
// 不绑定任何具体属性集类型，配任何含这些属性名的集即可（含编辑器生成的）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 伤害执行：最终伤害 = (来源 Damage + SetByCaller 伤害) − 目标 DamageNegation
    ///                  （格挡时再减 GuardDamageNegation），结果写入目标 IncomingDamage。
    /// IncomingDamage 再由生命属性集的 Meta 管线映射成 -Health。
    /// </summary>
    [CreateAssetMenu(fileName = "Exec_Damage", menuName = "Sigil/Combat/Damage Execution")]
    public class DamageExecutionCalculation : GameplayEffectExecutionCalculation
    {
        [Tooltip("SetByCaller 里代表附加伤害的标签（如 Data.Damage）")]
        public GameplayTag DamageSetByCallerTag = GameplayTag.RequestTag("Data.Damage");

        [Tooltip("目标拥有此标签时按格挡处理（额外减 GuardDamageNegation）")]
        public GameplayTag BlockingStateTag = GameplayTag.RequestTag("State.Blocking");

        [Header("属性名（跨属性集按名解析）")]
        public string SourceDamageName = "Damage";
        public string DamageNegationName = "DamageNegation";
        public string GuardDamageNegationName = "GuardDamageNegation";
        public string IncomingDamageName = "IncomingDamage";

        public override void Execute(GameplayEffectSpec spec, AbilitySystemComponent sourceASC, AbilitySystemComponent targetASC, List<GameplayExecutionOutput> outputs)
        {
            if (targetASC == null) return;

            // 基础伤害：来源 Damage 属性 + SetByCaller
            float damage = 0f;
            var srcDamage = sourceASC != null ? sourceASC.GetAttributeDataByName(SourceDamageName) : null;
            if (srcDamage != null) damage += srcDamage.CurrentValue;
            damage += spec.GetSetByCallerMagnitude(DamageSetByCallerTag, 0f);

            // 减伤：目标 DamageNegation（+ 格挡时 GuardDamageNegation）
            var negation = targetASC.GetAttributeDataByName(DamageNegationName);
            if (negation != null) damage -= negation.CurrentValue;
            if (BlockingStateTag.IsValid && targetASC.HasMatchingGameplayTag(BlockingStateTag))
            {
                var guard = targetASC.GetAttributeDataByName(GuardDamageNegationName);
                if (guard != null) damage -= guard.CurrentValue;
            }

            damage = Mathf.Max(0f, damage);
            if (damage <= 0f) return;

            // 写入目标 IncomingDamage（Meta），由生命属性集映射成 -Health
            var incoming = targetASC.FindAttributeByName(IncomingDamageName);
            if (incoming.IsValid)
                outputs.Add(new GameplayExecutionOutput(incoming, damage));
        }
    }
}
