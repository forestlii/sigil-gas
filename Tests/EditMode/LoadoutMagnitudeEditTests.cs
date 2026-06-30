// EditMode 测试：第1条 ASC/属性新功能——曲线表 magnitude / GrantLoadout 初始化(按 level) / 激活失败通知 / 激活组变更。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    public class LoadoutMagnitudeEditTests
    {
        [Test]
        public void CurveTable_Evaluate_ByLevel()
        {
            var table = ScriptableObject.CreateInstance<CurveTable>();
            table.AddRow("Health", AnimationCurve.Linear(1f, 100f, 3f, 200f)); // level2→150 线性

            Assert.AreEqual(100f, table.Evaluate("Health", 1f), 0.01f);
            Assert.AreEqual(150f, table.Evaluate("Health", 2f), 0.01f);
            Assert.AreEqual(200f, table.Evaluate("Health", 3f), 0.01f);
            Assert.AreEqual(-1f, table.Evaluate("Missing", 1f, -1f), 0.01f, "缺行应返回 fallback");

            Object.DestroyImmediate(table);
        }

        [Test]
        public void CurveTableMagnitude_SetsAttribute_ByLevel()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);

            var table = ScriptableObject.CreateInstance<CurveTable>();
            table.AddRow("MaxHealth", AnimationCurve.Linear(1f, 100f, 3f, 300f)); // level3→300

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.MaxHealthAttribute,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.CurveTableBased(table, "MaxHealth", 1f)
            });

            asc.ApplyGameplayEffectToSelf(ge, 3); // level 3 → 曲线[3]=300
            Assert.AreEqual(300f, health.MaxHealth.CurrentValue, 0.01f, "曲线表 magnitude 应按 level 查值设属性");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ge);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void GrantLoadout_AddsAttributeSet_AndInitsByLevel()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AttributeInitializeLevel = 2;

            var table = ScriptableObject.CreateInstance<CurveTable>();
            table.AddRow("MaxHealth", AnimationCurve.Linear(1f, 50f, 3f, 250f)); // level2→150

            var maxHealthAttr = new AS_Health().MaxHealthAttribute; // 句柄=类型+名，与实例无关
            var initGe = ScriptableObject.CreateInstance<GameplayEffect>();
            initGe.DurationType = EGameplayEffectDurationType.Instant;
            initGe.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = maxHealthAttr,
                Operation = EAttributeModifierOp.Override,
                Magnitude = GameplayModifierMagnitude.CurveTableBased(table, "MaxHealth", 1f)
            });

            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>();
            loadout.GrantedAttributeSetTypes.Add(typeof(AS_Health).FullName);
            loadout.GrantedEffects.Add(initGe);

            asc.GrantLoadout(loadout);

            var health = asc.GetAttributeSet<AS_Health>();
            Assert.IsNotNull(health, "loadout 应添加 AS_Health 属性集");
            Assert.AreEqual(150f, health.MaxHealth.CurrentValue, 0.01f, "初始化 GE 应按 AttributeInitializeLevel(2) 查曲线设初值");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(initGe);
            Object.DestroyImmediate(table);
            Object.DestroyImmediate(loadout);
        }

        [Test]
        public void ActivationFailed_FiresWithReason()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            EAbilityActivationFailReason reason = EAbilityActivationFailReason.None;
            bool fired = false;
            asc.OnAbilityActivationFailed += (ab, r) => { fired = true; reason = r; };

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Instant>();
            tmpl.AbilityTags.Add(GameplayTag.RequestTag("Ability.Test"));
            tmpl.ActivationBlockedTags.Add(GameplayTag.RequestTag("State.Stunned"));
            var handle = asc.GiveAbility(tmpl);

            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Stunned"));

            Assert.IsFalse(asc.TryActivateAbility(handle), "被 blocked tag 挡住应激活失败");
            Assert.IsTrue(fired, "应触发 OnAbilityActivationFailed");
            Assert.AreEqual(EAbilityActivationFailReason.ActivationBlockedTags, reason, "失败原因应为 ActivationBlockedTags");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
        }

        [Test]
        public void ChangeActivationGroup_UpdatesPolicy()
        {
            var go = new GameObject("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Hold>();
            tmpl.ActivationGroup = EAbilityActivationGroup.ExclusiveReplaceable;
            var handle = asc.GiveAbility(tmpl);
            var ability = asc.FindAbilitySpec(handle).Ability;

            Assert.IsTrue(asc.CanChangeActivationGroup(EAbilityActivationGroup.ExclusiveBlocking, ability), "无冲突时应可换组");
            Assert.IsTrue(asc.ChangeActivationGroup(EAbilityActivationGroup.ExclusiveBlocking, ability), "换组应成功");
            Assert.AreEqual(EAbilityActivationGroup.ExclusiveBlocking, ability.ActivationGroup, "ActivationGroup 应更新为 ExclusiveBlocking");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl);
        }
    }
}
