// EditMode 测试：验证移植后的核心 GAS 行为。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 测试用具体技能（OnActivateAbility 后立即结束）
    public class TestAbility_Instant : GameplayAbility
    {
        public bool Activated;
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            Activated = true;
            EndAbility();
        }
    }

    // 测试用具体技能（激活后保持，不结束 —— 验证激活组互斥）
    public class TestAbility_Hold : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) { /* 保持激活 */ }
    }

    public class CoreGasTests
    {
        [Test]
        public void Tag_Hierarchy_Matching()
        {
            var child = GameplayTag.RequestTag("State.Combat.Attacking");
            var parent = GameplayTag.RequestTag("State.Combat");
            Assert.IsTrue(child.MatchesTag(parent), "子标签应命中父标签");
            Assert.IsFalse(parent.MatchesTag(child), "父标签不应命中子标签");

            var c = new GameplayTagContainer();
            c.AddTag(child);
            Assert.IsTrue(c.HasTag(parent), "容器含子标签时 HasTag(父) 应为真");
            Assert.IsFalse(c.HasTagExact(parent), "HasTagExact(父) 应为假");
        }

        [Test]
        public void LooseTag_ParentCountPropagation()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.A.B"));

            Assert.IsTrue(asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State.A.B")));
            Assert.IsTrue(asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State.A")), "加子标签后父标签应可查到");
            Assert.IsTrue(asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State")));

            asc.RemoveLooseGameplayTag(GameplayTag.RequestTag("State.A.B"));
            Assert.IsFalse(asc.HasMatchingGameplayTag(GameplayTag.RequestTag("State.A")), "移除后父标签计数应归零");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Damage_MetaPipeline_ReducesHealth()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            // 运行时造一个瞬时伤害 GE：对 IncomingDamage +25
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.IncomingDamageAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(25f)
            });

            float before = health.Health.CurrentValue; // 100
            asc.ApplyGameplayEffectToSelf(ge);

            Assert.AreEqual(before - 25f, health.Health.CurrentValue, 0.01f, "IncomingDamage 应映射成 -Health");
            Assert.AreEqual(0f, health.IncomingDamage.BaseValue, 0.01f, "IncomingDamage 应被清零");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
        }

        [Test]
        public void Health_ClampedToMax()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            // 治疗 999 → IncomingHealing → +Health，但应被 clamp 到 MaxHealth(100)
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.IncomingHealingAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(999f)
            });
            asc.ApplyGameplayEffectToSelf(ge);

            Assert.LessOrEqual(health.Health.CurrentValue, 100f, "Health 不应超过 MaxHealth");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
        }

        [Test]
        public void Ability_GiveAndActivate()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Instant>();
            tmpl.AbilityTags.Add(GameplayTag.RequestTag("Ability.Test"));
            var handle = asc.GiveAbility(tmpl);

            Assert.IsTrue(asc.TryActivateAbility(handle), "应能激活");
            var spec = asc.FindAbilitySpec(handle);
            Assert.IsTrue(((TestAbility_Instant)spec.Ability).Activated, "OnActivateAbility 应已执行");
            Assert.IsFalse(spec.IsActive, "瞬时技能激活后应已结束");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
        }

        [Test]
        public void Ability_ActivateByTag()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Instant>();
            tmpl.AbilityTags.Add(GameplayTag.RequestTag("Ability.Slide"));
            asc.GiveAbility(tmpl);

            Assert.IsTrue(asc.TryActivateAbilitiesByTag(GameplayTag.RequestTag("Ability.Slide")), "按标签激活应成功");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
        }

        [Test]
        public void ActivationPolicy_BlockingPreventsExclusive()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var blocking = ScriptableObject.CreateInstance<TestAbility_Hold>();
            blocking.ActivationPolicy = EAbilityActivationPolicy.Blocking;
            var hBlock = asc.GiveAbility(blocking);

            var replaceable = ScriptableObject.CreateInstance<TestAbility_Hold>();
            replaceable.ActivationPolicy = EAbilityActivationPolicy.Replaceable;
            var hRepl = asc.GiveAbility(replaceable);

            Assert.IsTrue(asc.TryActivateAbility(hBlock), "Blocking 技能应能先激活");
            Assert.IsFalse(asc.TryActivateAbility(hRepl), "Blocking 激活中，Replaceable 应被阻挡");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(blocking);
            Object.DestroyImmediate(replaceable);
        }
    }
}
