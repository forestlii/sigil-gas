// EditMode 测试：档2 小偏差批 #19 DynamicTags / #22 ExternalConfirm / #23 Custom 确认 / #35 GE 结算事件。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 测试用目标采集器：记录 ConfirmTargeting 调用次数。
    public class TestTargetActor : TargetActor
    {
        public int ConfirmCount;
        protected override void DoTrace(List<TargetHitResult> outHits, Vector3 start, Vector3 end) { }
        public override void ConfirmTargeting() { ConfirmCount++; base.ConfirmTargeting(); }
    }

    public class Batch2SmallDeviationsEditTests
    {
        private static GameplayTag T(string s) => GameplayTag.RequestTag(s);

        // ---- #19 DynamicTags ----
        [Test]
        public void GrantLoadout_DynamicTags_AppendedToAbility()
        {
            var go = new GameObject("ASC"); var asc = go.AddComponent<AbilitySystemComponent>();

            var tmpl = ScriptableObject.CreateInstance<TestAbility_Hold>();
            tmpl.AbilityTags.Add(T("Ability.Melee"));

            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>();
            loadout.GrantedAbilities.Add(new AbilityLoadout.GrantedAbility
            {
                Ability = tmpl, Level = 1,
                DynamicTags = new List<GameplayTag> { T("Slot.Primary") }
            });

            var handles = asc.GrantLoadout(loadout);
            var ability = asc.FindAbilitySpec(handles.AbilityHandles[0]).Ability;

            Assert.IsTrue(ability.GetAbilityTags().HasTag(T("Ability.Melee")), "原身份标签保留");
            Assert.IsTrue(ability.GetAbilityTags().HasTag(T("Slot.Primary")), "DynamicTags 应附加到授予的技能实例");
            // 模板不受污染
            Assert.IsFalse(tmpl.AbilityTags.Contains(T("Slot.Primary")), "模板不应被改");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(tmpl); Object.DestroyImmediate(loadout);
        }

        // ---- #22 ExternalConfirm ----
        [Test]
        public void AbilityTask_ExternalConfirm_EndsTaskWhenRequested()
        {
            var go = new GameObject("ASC"); var asc = go.AddComponent<AbilitySystemComponent>();
            var tmpl = ScriptableObject.CreateInstance<TestAbility_Hold>();
            var h = asc.GiveAbility(tmpl);
            asc.TryActivateAbility(h);
            var ability = asc.FindAbilitySpec(h).Ability;

            var task = AbilityTask_WaitGameplayEvent.WaitGameplayEvent(ability, T("Combat.HitConfirm"));
            task.Activate();
            Assert.IsTrue(task.IsActive, "激活后任务应在跑");

            task.ExternalConfirm(endTask: true);
            Assert.IsFalse(task.IsActive, "ExternalConfirm(endTask:true) 应结束任务");

            Object.DestroyImmediate(go); Object.DestroyImmediate(tmpl);
        }

        // ---- #23 Custom 确认 ----
        [Test]
        public void TargetingConfirmation_Custom_DoesNotAutoConfirm()
        {
            var instant = new TestTargetActor { ConfirmationType = EGameplayTargetingConfirmation.Instant };
            instant.StartTargeting(null);
            Assert.AreEqual(1, instant.ConfirmCount, "Instant 应在 StartTargeting 时自动确认一次");

            var custom = new TestTargetActor { ConfirmationType = EGameplayTargetingConfirmation.Custom };
            custom.StartTargeting(null);
            Assert.AreEqual(0, custom.ConfirmCount, "Custom 不应自动确认（交由外部择机）");
            custom.ConfirmTargeting();
            Assert.AreEqual(1, custom.ConfirmCount, "外部确认后应确认一次");
        }

        // ---- #35 组件级 OnPostGameplayEffectExecute ----
        [Test]
        public void OnPostGameplayEffectExecute_FiresOnExecute()
        {
            var go = new GameObject("ASC"); var asc = go.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health(); asc.AddAttributeSet(health);

            int fired = 0;
            asc.OnPostGameplayEffectExecute += _ => fired++;

            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = health.IncomingDamageAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(10f)
            });
            asc.ApplyGameplayEffectToSelf(ge);

            Assert.GreaterOrEqual(fired, 1, "GE 结算后组件级事件应触发");

            Object.DestroyImmediate(go); Object.DestroyImmediate(ge);
        }
    }
}
