// EditMode 测试：#34 攻击类型标签注入效果 spec（修死配置 AttackDefinition.AttackTags）。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class AttackTagInjectionEditTests
    {
        private static GameplayTag T(string s) => GameplayTag.RequestTag(s);

        [Test]
        public void Spec_GetAllAssetTags_CombinesDefAndDynamic()
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.AssetTags.Add(T("Effect.Damage"));

            var spec = new GameplayEffectSpec(ge, new GameplayEffectContext());
            spec.AddDynamicAssetTags(new[] { T("Attack.Melee"), T("Attack.Slash") });

            var all = new System.Collections.Generic.List<GameplayTag>(spec.GetAllAssetTags());
            Assert.Contains(T("Effect.Damage"), all, "应含定义静态 AssetTags");
            Assert.Contains(T("Attack.Melee"), all, "应含动态注入标签");
            Assert.Contains(T("Attack.Slash"), all);

            Object.DestroyImmediate(ge);
        }

        [Test]
        public void ApplyAttack_InjectsAttackTagsIntoSpec()
        {
            var srcGo = new GameObject("Src"); var src = srcGo.AddComponent<AbilitySystemComponent>();
            var tgtGo = new GameObject("Tgt"); var tgt = tgtGo.AddComponent<AbilitySystemComponent>();

            // 持续型伤害 GE（持续才会登记为 active，便于回查 spec）
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.HasDuration;
            ge.Duration = 10f;

            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            attack.TargetEffect = ge;
            attack.AttackTags.Add(T("Attack.Melee"));
            attack.AttackTags.Add(T("Attack.Heavy"));

            AttackApplication.ApplyAttack(attack, src, srcGo, tgt, Vector3.zero);

            var actives = tgt.GetActiveGameplayEffects();
            Assert.AreEqual(1, actives.Count, "应施加一个持续效果");
            var all = new System.Collections.Generic.List<GameplayTag>(actives[0].Spec.GetAllAssetTags());
            Assert.Contains(T("Attack.Melee"), all, "攻击类型应注入到效果 spec（#34 死配置修复）");
            Assert.Contains(T("Attack.Heavy"), all);

            Object.DestroyImmediate(srcGo); Object.DestroyImmediate(tgtGo);
            Object.DestroyImmediate(ge); Object.DestroyImmediate(attack);
        }

        [Test]
        public void RemoveEffectsWithTags_HonorsDynamicAssetTags()
        {
            var go = new GameObject("ASC"); var asc = go.AddComponent<AbilitySystemComponent>();

            // 持续效果：动态注入 Attack.Melee（定义本身无 AssetTags）
            var buff = ScriptableObject.CreateInstance<GameplayEffect>();
            buff.DurationType = EGameplayEffectDurationType.HasDuration;
            buff.Duration = 10f;
            var buffSpec = asc.MakeOutgoingSpec(buff);
            buffSpec.AddDynamicAssetTags(new[] { T("Attack.Melee") });
            asc.ApplyGameplayEffectSpecToSelf(buffSpec);
            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count);

            // 瞬时效果：移除带 Attack.Melee 的效果 → 应按动态资产标签命中并移除上面的持续效果
            var cleanse = ScriptableObject.CreateInstance<GameplayEffect>();
            cleanse.DurationType = EGameplayEffectDurationType.Instant;
            cleanse.RemoveEffectsWithTags.Add(T("Attack.Melee"));
            asc.ApplyGameplayEffectToSelf(cleanse);

            Assert.AreEqual(0, asc.GetActiveGameplayEffects().Count, "RemoveEffectsWithTags 应认动态注入的资产标签");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(buff); Object.DestroyImmediate(cleanse);
        }
    }
}
