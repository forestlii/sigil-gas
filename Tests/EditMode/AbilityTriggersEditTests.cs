// EditMode 测试：AbilityTriggers（事件触发激活）——GameplayEvent / OwnedTagAdded / OwnedTagPresent 三种触发源。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 记录激活次数与触发数据的测试技能（激活后立即结束）。
    public class TestAbility_RecordTrigger : GameplayAbility
    {
        public int ActivateCount;
        public GameplayEventData LastTrigger;
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            ActivateCount++;
            LastTrigger = triggerData;
            EndAbility();
        }
    }

    public class AbilityTriggersEditTests
    {
        private static AbilitySystemComponent MakeASC(out GameObject go)
        {
            go = new GameObject("ASC");
            return go.AddComponent<AbilitySystemComponent>();
        }

        private static T GiveWithTrigger<T>(AbilitySystemComponent asc, string triggerTag, EGameplayAbilityTriggerSource source)
            where T : GameplayAbility
        {
            var tmpl = ScriptableObject.CreateInstance<T>();
            tmpl.AbilityTriggers.Add(new AbilityTrigger
            {
                TriggerTag = GameplayTag.RequestTag(triggerTag),
                TriggerSource = source
            });
            var handle = asc.GiveAbility(tmpl);
            return (T)asc.FindAbilitySpec(handle).Ability; // 返回克隆实例
        }

        [Test]
        public void GameplayEvent_TriggersActivation()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "Event.Hit", EGameplayAbilityTriggerSource.GameplayEvent);

            asc.SendGameplayEvent(GameplayTag.RequestTag("Event.Hit"));

            Assert.AreEqual(1, ab.ActivateCount, "收到匹配 GameplayEvent 应激活一次");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GameplayEvent_HierarchicalMatch_ChildEventHitsParentTrigger()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "Event.Combat", EGameplayAbilityTriggerSource.GameplayEvent);

            asc.SendGameplayEvent(GameplayTag.RequestTag("Event.Combat.Melee")); // 子事件命中父监听 tag

            Assert.AreEqual(1, ab.ActivateCount, "子事件应层级命中父 TriggerTag");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GameplayEvent_PassesEventDataAsTriggerData()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "Event.X", EGameplayAbilityTriggerSource.GameplayEvent);

            asc.SendGameplayEvent(GameplayTag.RequestTag("Event.X"));

            Assert.IsNotNull(ab.LastTrigger, "应把事件数据作为 triggerData 传入");
            Assert.AreEqual(GameplayTag.RequestTag("Event.X"), ab.LastTrigger.EventTag, "triggerData 应携带事件 tag");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GameplayEvent_NonMatching_DoesNotActivate()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "Event.Hit", EGameplayAbilityTriggerSource.GameplayEvent);

            asc.SendGameplayEvent(GameplayTag.RequestTag("Event.Other"));

            Assert.AreEqual(0, ab.ActivateCount, "不匹配的事件不应触发激活");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OwnedTagAdded_TriggersActivationWhenTagAppears()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "State.Enraged", EGameplayAbilityTriggerSource.OwnedTagAdded);

            Assert.AreEqual(0, ab.ActivateCount, "授予时标签未出现，不应激活");
            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Enraged"));

            Assert.AreEqual(1, ab.ActivateCount, "标签出现应激活");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OwnedTagAdded_DoesNotReactivateOnSecondStack()
        {
            var asc = MakeASC(out var go);
            var ab = GiveWithTrigger<TestAbility_RecordTrigger>(asc, "State.Enraged", EGameplayAbilityTriggerSource.OwnedTagAdded);

            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Enraged")); // 0→1：激活
            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Enraged")); // 1→2：存在性未翻转，不应再触发

            Assert.AreEqual(1, ab.ActivateCount, "标签计数 1→2 未翻转存在性，不应重复触发");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OwnedTagPresent_CancelsAbilityWhenTagRemoved()
        {
            var asc = MakeASC(out var go);
            // Hold 技能：激活后保持不结束，验证标签消失时被取消
            var ab = GiveWithTrigger<TestAbility_Hold>(asc, "State.Channeling", EGameplayAbilityTriggerSource.OwnedTagPresent);

            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Channeling"));
            Assert.IsTrue(ab.IsActive, "标签出现应激活并保持");

            asc.RemoveLooseGameplayTag(GameplayTag.RequestTag("State.Channeling"));
            Assert.IsFalse(ab.IsActive, "OwnedTagPresent：标签消失应取消技能");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void OwnedTagAdded_DoesNotCancelOnTagRemoved()
        {
            var asc = MakeASC(out var go);
            // OwnedTagAdded 语义只管"出现即激活一次"，不负责消失时取消
            var ab = GiveWithTrigger<TestAbility_Hold>(asc, "State.Marked", EGameplayAbilityTriggerSource.OwnedTagAdded);

            asc.AddLooseGameplayTag(GameplayTag.RequestTag("State.Marked"));
            Assert.IsTrue(ab.IsActive, "标签出现应激活");

            asc.RemoveLooseGameplayTag(GameplayTag.RequestTag("State.Marked"));
            Assert.IsTrue(ab.IsActive, "OwnedTagAdded 不应因标签消失而取消");
            Object.DestroyImmediate(go);
        }
    }
}
