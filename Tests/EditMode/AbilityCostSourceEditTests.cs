// EditMode 测试：#15 模块化额外消耗（AdditionalCosts）+ #21 属性变更事件携带来源。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 测试用充能消耗：带计数，CheckCost 看是否>0，ApplyCost 扣一次。
    public class TestChargeCost : AbilityCost
    {
        public int Charges = 1;
        public int CheckCalls;
        public int ApplyCalls;
        public override bool CheckCost(GameplayAbility ability) { CheckCalls++; return Charges > 0; }
        public override void ApplyCost(GameplayAbility ability) { ApplyCalls++; Charges--; }
    }

    // 测试用技能：激活时显式 CommitAbility（施加消耗+冷却，对齐 UE），随即结束。
    public class TestAbility_Commit : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            CommitAbility();
            EndAbility();
        }
    }

    public class AbilityCostSourceEditTests
    {
        // ---- #15 AdditionalCosts ----

        [Test]
        public void AdditionalCost_DepletedBlocksActivation()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Instant>();
            var cost = ScriptableObject.CreateInstance<TestChargeCost>();
            cost.Charges = 0; // 没充能
            tmpl.AdditionalCosts.Add(cost);
            var handle = asc.GiveAbility(tmpl);

            Assert.IsFalse(asc.TryActivateAbility(handle), "额外消耗买不起时应无法激活");
            var spec = asc.FindAbilitySpec(handle);
            Assert.IsFalse(((TestAbility_Instant)spec.Ability).Activated, "激活逻辑不应执行");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
            Object.DestroyImmediate(cost);
        }

        [Test]
        public void AdditionalCost_AppliedOnActivate()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Commit>();
            var cost = ScriptableObject.CreateInstance<TestChargeCost>();
            cost.Charges = 2;
            tmpl.AdditionalCosts.Add(cost);
            var handle = asc.GiveAbility(tmpl);

            // 激活会 CommitAbility → ApplyCost（实例克隆后的 cost）
            Assert.IsTrue(asc.TryActivateAbility(handle), "有充能应能激活");
            var clonedCost = (TestChargeCost)asc.FindAbilitySpec(handle).Ability.AdditionalCosts[0];
            Assert.AreEqual(1, clonedCost.Charges, "激活应扣一次充能（2→1）");
            Assert.AreNotSame(cost, clonedCost, "额外消耗应按技能实例克隆，不共享模板");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
            Object.DestroyImmediate(cost);
        }

        [Test]
        public void AdditionalCost_OnlyApplyCostOnHit_DeferredToHit()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Hold>();
            var cost = ScriptableObject.CreateInstance<TestChargeCost>();
            cost.Charges = 3;
            cost.OnlyApplyCostOnHit = true; // 命中才扣
            tmpl.AdditionalCosts.Add(cost);
            var handle = asc.GiveAbility(tmpl);
            var ability = asc.FindAbilitySpec(handle).Ability;
            var clonedCost = (TestChargeCost)ability.AdditionalCosts[0];

            Assert.IsTrue(asc.TryActivateAbility(handle), "应能激活");
            Assert.AreEqual(3, clonedCost.Charges, "OnlyApplyCostOnHit 激活时不应扣");

            ability.ApplyOnHitCosts(); // 命中
            Assert.AreEqual(2, clonedCost.Charges, "命中后应扣一次（3→2）");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
            Object.DestroyImmediate(cost);
        }

        // ---- #21 属性变更事件携带来源 ----

        [Test]
        public void AttributeChanged_CarriesSourceContext()
        {
            var attackerGo = new GameObject("Attacker");
            var attacker = attackerGo.AddComponent<AbilitySystemComponent>();

            var victimGo = new GameObject("Victim");
            var victim = victimGo.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            victim.AddAttributeSet(health);

            AttributeChangeData? healthChange = null;
            victim.OnAttributeChanged += data =>
            {
                if (data.Attribute == health.HealthAttribute) healthChange = data;
            };

            // 攻击者制作伤害 spec（来源=attacker），施加到受害者
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.IncomingDamageAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(15f)
            });
            var spec = attacker.MakeOutgoingSpec(ge);
            victim.ApplyGameplayEffectSpecToSelf(spec);

            Assert.IsTrue(healthChange.HasValue, "Health 变更事件应触发");
            Assert.IsNotNull(healthChange.Value.Source, "属性变更应携带来源上下文");
            Assert.AreSame(attacker, healthChange.Value.Source.SourceASC, "来源应为攻击者 ASC");

            Object.DestroyImmediate(attackerGo);
            Object.DestroyImmediate(victimGo);
            Object.DestroyImmediate(ge);
        }
    }
}
