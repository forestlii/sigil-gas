// PlayMode 测试：技能激活的重入安全（C1）——激活临界区锁 spec.IsActivating。
//  C1) 技能 A 激活时取消技能 B，B 的 OnEndAbility 回调里对同一句柄重入激活 A →
//      A 只应激活一次（IsActive 置位晚于取消回调，靠 IsActivating 挡住这段窗口的重入）。
//  另附回归：临界区锁正常复位——A 结束后能再次激活（标志不残留）。
// 放 PlayMode：GiveAbility / Object.Destroy 清理，EditMode 禁用。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 激活即计数、保持 active（不自动结束）——用于数"激活了几次"。
    public class CountingAbility : GameplayAbility
    {
        public int ActivateCount;
        protected override void OnActivateAbility(GameplayEventData triggerData) { ActivateCount++; }
    }

    // 结束（被取消）时执行注入的动作——用于在取消回调里制造重入。
    public class ReentrantOnEndAbility : GameplayAbility
    {
        public System.Action OnEndCallback;
        protected override void OnActivateAbility(GameplayEventData triggerData) { /* 保持激活 */ }
        protected override void OnEndAbility(bool wasCancelled) { OnEndCallback?.Invoke(); }
    }

    public class AbilityActivationReentrancyPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private AbilitySystemComponent NewASC()
        {
            var go = new GameObject("ASC"); _spawned.Add(go);
            return go.AddComponent<AbilitySystemComponent>();
        }

        private T NewAbility<T>(string abilityTag) where T : GameplayAbility
        {
            var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a);
            a.AbilityTags.Add(Tag(abilityTag));
            return a;
        }

        private AbilityInteractionRules NewRules()
        {
            var r = ScriptableObject.CreateInstance<AbilityInteractionRules>(); _assets.Add(r);
            return r;
        }

        // A 激活时 cancel 带 targetTag 的技能
        private static AbilityTagRule CancelRule(string sourceTag, string cancelTag)
        {
            return new AbilityTagRule
            {
                AbilityTag = GameplayTag.RequestTag(sourceTag),
                AbilityTagsToBlock = new List<GameplayTag>(),
                AbilityTagsToCancel = new List<GameplayTag> { GameplayTag.RequestTag(cancelTag) },
                ActivationRequiredTags = new List<GameplayTag>(),
                ActivationBlockedTags = new List<GameplayTag>()
            };
        }

        private static int LooseCount(AbilitySystemComponent asc, GameplayTag tag)
        {
            var list = new List<KeyValuePair<GameplayTag, int>>();
            asc.GetOwnedGameplayTagCounts(list);
            foreach (var kv in list) if (kv.Key.Equals(tag)) return kv.Value;
            return 0;
        }

        // ============ C1：取消回调里重入激活同句柄 → 只激活一次 ============
        [UnityTest]
        public IEnumerator C1_ReentrantActivationInCancelCallback_ActivatesOnce()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(CancelRule("Ability.A", "Ability.B")); // A 激活时取消 B
            asc.SetInteractionRules(rules);

            var aTemplate = NewAbility<CountingAbility>("Ability.A");
            aTemplate.ActivationOwnedLooseTags.Add(new GameplayTagCount(Tag("State.A"), 1)); // 激活期间挂 State.A
            var bTemplate = NewAbility<ReentrantOnEndAbility>("Ability.B");

            var hA = asc.GiveAbility(aTemplate);
            var hB = asc.GiveAbility(bTemplate);

            // GiveAbility 克隆模板成运行时实例——激活/回调作用在克隆上，断言与注入必须取克隆
            var a = (CountingAbility)asc.FindAbilitySpec(hA).Ability;
            var b = (ReentrantOnEndAbility)asc.FindAbilitySpec(hB).Ability;

            Assert.IsTrue(asc.TryActivateAbility(hB), "B 先激活");

            // B 被 A 取消时，在其结束回调里重入激活 A（此刻 A 尚未置 IsActive）
            bool reentrantResult = true;
            b.OnEndCallback = () => reentrantResult = asc.TryActivateAbility(hA);

            Assert.IsTrue(asc.TryActivateAbility(hA), "A 应激活成功");

            Assert.IsFalse(reentrantResult, "取消回调里对同句柄的重入激活应被临界区锁挡下（返回 false）");
            Assert.AreEqual(1, a.ActivateCount, "A 只应激活一次（重入不得造成双激活）");
            Assert.AreEqual(1, LooseCount(asc, Tag("State.A")), "激活期松散标签只应挂一次（不翻倍）");
            Assert.IsFalse(asc.FindAbilitySpec(hB).Ability.IsActive, "B 应已被取消");
            yield return null;
        }

        // ============ 回归：临界区锁正常复位——结束后能再次激活 ============
        [UnityTest]
        public IEnumerator IsActivatingResetsAfterActivation_CanReactivate()
        {
            var asc = NewASC();
            var hA = asc.GiveAbility(NewAbility<CountingAbility>("Ability.A"));
            var a = (CountingAbility)asc.FindAbilitySpec(hA).Ability; // 运行时克隆实例

            Assert.IsTrue(asc.TryActivateAbility(hA), "首次激活");
            Assert.AreEqual(1, a.ActivateCount);
            a.EndAbility();

            Assert.IsTrue(asc.TryActivateAbility(hA), "结束后应能再次激活（IsActivating 未残留）");
            Assert.AreEqual(2, a.ActivateCount, "第二次激活应正常计数");
            yield return null;
        }
    }
}
