// PlayMode 测试：AbilityInteractionRules.AbilityTagsToBlock —— "本技能激活期间阻挡带这些标签的技能激活"。
//  A) A 激活 → B 被挡；B) A 结束 → B 可激活；C) block 不波及无关技能；
//  D) 引用计数（两个来源挡同一标签，结束一个仍挡，全结束才解）；E) A 从未激活 → B 不被挡（block 仅在激活期间）；
//  F) 回归：AbilityTagsToCancel 仍能取消已激活技能（与新增 block 通道互不干扰）。
// 放 PlayMode：ClearAbility / 测试涉及 Object.Destroy 清理，EditMode 禁用。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 激活后保持 active（不自动结束），由测试显式 EndAbility 结束。
    public class HoldingAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) { /* 保持激活 */ }
    }

    public class AbilityBlockTagsPlayTests
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

        private HoldingAbility NewAbility(string abilityTag)
        {
            var a = ScriptableObject.CreateInstance<HoldingAbility>(); _assets.Add(a);
            a.AbilityTags.Add(Tag(abilityTag));
            return a;
        }

        private AbilityInteractionRules NewRules()
        {
            var r = ScriptableObject.CreateInstance<AbilityInteractionRules>(); _assets.Add(r);
            return r;
        }

        // 一条规则：技能标签 source 激活时 block / cancel 带 targetTag 的技能
        private static AbilityTagRule Rule(string sourceTag, string blockTag = null, string cancelTag = null)
        {
            var rule = new AbilityTagRule
            {
                AbilityTag = GameplayTag.RequestTag(sourceTag),
                AbilityTagsToBlock = new List<GameplayTag>(),
                AbilityTagsToCancel = new List<GameplayTag>(),
                ActivationRequiredTags = new List<GameplayTag>(),
                ActivationBlockedTags = new List<GameplayTag>()
            };
            if (blockTag != null) rule.AbilityTagsToBlock.Add(GameplayTag.RequestTag(blockTag));
            if (cancelTag != null) rule.AbilityTagsToCancel.Add(GameplayTag.RequestTag(cancelTag));
            return rule;
        }

        // ============ A) A 激活 → B 被挡 ============
        [UnityTest]
        public IEnumerator A_ActiveSourceBlocksTargetActivation()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(Rule("Ability.A", blockTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            var hA = asc.GiveAbility(NewAbility("Ability.A"));
            var hB = asc.GiveAbility(NewAbility("Ability.B"));

            Assert.IsTrue(asc.TryActivateAbility(hA), "A 应能激活");
            Assert.IsTrue(asc.FindAbilitySpec(hA).Ability.IsActive, "A 激活后应保持 active");
            Assert.IsFalse(asc.TryActivateAbility(hB), "A 激活期间 B 应被 block 挡住，不能激活");
            Assert.IsFalse(asc.FindAbilitySpec(hB).Ability.IsActive, "B 应仍未激活");
            yield return null;
        }

        // ============ B) A 结束 → block 解除，B 可激活 ============
        [UnityTest]
        public IEnumerator B_BlockLiftedAfterSourceEnds()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(Rule("Ability.A", blockTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            var hA = asc.GiveAbility(NewAbility("Ability.A"));
            var hB = asc.GiveAbility(NewAbility("Ability.B"));

            asc.TryActivateAbility(hA);
            Assert.IsFalse(asc.TryActivateAbility(hB), "A 激活期间 B 被挡");

            asc.FindAbilitySpec(hA).Ability.EndAbility(); // A 结束
            Assert.IsTrue(asc.TryActivateAbility(hB), "A 结束后 block 解除，B 应能激活");
            Assert.IsTrue(asc.FindAbilitySpec(hB).Ability.IsActive, "B 应已激活");
            yield return null;
        }

        // ============ C) block 不波及无关技能 ============
        [UnityTest]
        public IEnumerator C_BlockDoesNotAffectUnrelatedAbility()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(Rule("Ability.A", blockTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            var hA = asc.GiveAbility(NewAbility("Ability.A"));
            var hC = asc.GiveAbility(NewAbility("Ability.C")); // 不在 block 列表

            asc.TryActivateAbility(hA);
            Assert.IsTrue(asc.TryActivateAbility(hC), "无关技能 C 不应被 A 的 block 影响");
            yield return null;
        }

        // ============ D) 引用计数：两个来源挡同一标签 ============
        [UnityTest]
        public IEnumerator D_BlockIsRefCountedAcrossSources()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(Rule("Ability.A1", blockTag: "Ability.B"));
            rules.AddBaseRule(Rule("Ability.A2", blockTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            var hA1 = asc.GiveAbility(NewAbility("Ability.A1"));
            var hA2 = asc.GiveAbility(NewAbility("Ability.A2"));
            var hB = asc.GiveAbility(NewAbility("Ability.B"));

            asc.TryActivateAbility(hA1);
            asc.TryActivateAbility(hA2);
            Assert.IsFalse(asc.TryActivateAbility(hB), "两个来源都挡 B，B 不能激活");

            asc.FindAbilitySpec(hA1).Ability.EndAbility(); // 只结束一个来源
            Assert.IsFalse(asc.TryActivateAbility(hB), "仍有 A2 挡着，B 应仍不能激活（引用计数）");

            asc.FindAbilitySpec(hA2).Ability.EndAbility(); // 结束最后一个来源
            Assert.IsTrue(asc.TryActivateAbility(hB), "两来源都结束后 block 全解，B 应能激活");
            yield return null;
        }

        // ============ E) A 从未激活 → B 不被挡 ============
        [UnityTest]
        public IEnumerator E_InactiveSourceDoesNotBlock()
        {
            var asc = NewASC();
            var rules = NewRules();
            rules.AddBaseRule(Rule("Ability.A", blockTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            asc.GiveAbility(NewAbility("Ability.A")); // 授予但不激活
            var hB = asc.GiveAbility(NewAbility("Ability.B"));

            Assert.IsTrue(asc.TryActivateAbility(hB), "A 未激活 → block 不生效，B 应能激活");
            yield return null;
        }

        // ============ F) 回归：cancel 通道仍正常 ============
        [UnityTest]
        public IEnumerator F_CancelChannelStillWorks()
        {
            var asc = NewASC();
            var rules = NewRules();
            // A 激活时取消已激活的 B（cancel，不是 block）
            rules.AddBaseRule(Rule("Ability.A", cancelTag: "Ability.B"));
            asc.SetInteractionRules(rules);

            var hA = asc.GiveAbility(NewAbility("Ability.A"));
            var hB = asc.GiveAbility(NewAbility("Ability.B"));

            Assert.IsTrue(asc.TryActivateAbility(hB), "B 先激活");
            Assert.IsTrue(asc.FindAbilitySpec(hB).Ability.IsActive, "B 现为 active");

            Assert.IsTrue(asc.TryActivateAbility(hA), "A 激活");
            Assert.IsFalse(asc.FindAbilitySpec(hB).Ability.IsActive, "A 的 cancel 应已取消 B");
            yield return null;
        }
    }
}
