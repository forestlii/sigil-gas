// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 把一个 AttackDefinition 施加到目标 ASC 的共享逻辑（近战/子弹/其他攻击源复用）。
// 抽出"主效果 + SetByCaller + 效果容器 + 命中 cue"这段核心施加，避免各攻击源重复。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>攻击施加工具：按 AttackDefinition 给目标施加伤害效果与命中表现。</summary>
    public static class AttackApplication
    {
        /// <summary>
        /// 对 <paramref name="targetASC"/> 施加 <paramref name="attack"/> 的主效果（带 SetByCaller）、
        /// 效果容器与命中 cue。来源 ASC / 命中点写进 EffectContext。
        /// </summary>
        public static void ApplyAttack(AttackDefinition attack, AbilitySystemComponent sourceASC,
            GameObject causer, AbilitySystemComponent targetASC, Vector3 hitPoint)
        {
            if (attack == null || targetASC == null) return;

            var context = new GameplayEffectContext(sourceASC)
            {
                HitLocation = hitPoint,
                HasHitLocation = true,
                EffectCauser = causer
            };
            int level = attack.TargetEffectLevel >= 1 ? attack.TargetEffectLevel : 1;

            // 主效果（伤害 GE）
            if (attack.TargetEffect != null)
            {
                var spec = new GameplayEffectSpec(attack.TargetEffect, context, level);
                spec.AddDynamicAssetTags(attack.AttackTags); // 攻击类型作动态资产标签注入 spec
                foreach (var sbc in attack.SetByCallerMagnitudes)
                    spec.SetSetByCallerMagnitude(sbc.Tag, sbc.Value);
                targetASC.ApplyGameplayEffectSpecToSelf(spec);
            }

            // 效果容器：批量额外效果
            if (attack.TargetEffectContainer.TargetGameplayEffects != null)
            {
                foreach (var ge in attack.TargetEffectContainer.TargetGameplayEffects)
                {
                    if (ge == null) continue;
                    var spec = new GameplayEffectSpec(ge, context, level);
                    spec.AddDynamicAssetTags(attack.AttackTags);
                    foreach (var sbc in attack.SetByCallerMagnitudes)
                        spec.SetSetByCallerMagnitude(sbc.Tag, sbc.Value);
                    targetASC.ApplyGameplayEffectSpecToSelf(spec);
                }
            }

            // 命中表现 cue
            foreach (var cue in attack.TargetGameplayCues)
                targetASC.SendGameplayEvent(cue, new GameplayEventData(cue) { Target = targetASC.gameObject, Context = context });
        }
    }
}
