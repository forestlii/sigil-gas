// PlayMode 测试：AbilityTask_PlayMontageAndWaitForEvent。
//  核心逻辑不依赖真实 Animator 渲染（animator 传 null，按 duration 计时驱动），验：
//   A) duration 后 OnCompleted；B) 匹配事件 OnEventReceived（含多次/忽略不匹配）；
//   C) OnBlendOut 早于 OnCompleted；D) 技能结束 → OnCancelled、不再 OnCompleted；
//   E) 无效输入（无 animator 且 duration<=0）立即 OnCancelled。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 空 host：激活后保持激活，由测试手动挂 PlayMontage 任务。
    public class MontageHostAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) { /* 保持激活 */ }
    }

    public class PlayMontagePlayTests
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

        private MontageHostAbility ActivateHost(out AbilitySystemComponent asc)
        {
            var go = new GameObject("Caster"); _spawned.Add(go);
            asc = go.AddComponent<AbilitySystemComponent>();
            var template = ScriptableObject.CreateInstance<MontageHostAbility>(); _assets.Add(template);
            template.AbilityTags.Add(Tag("Ability.MontageHost"));
            var handle = asc.GiveAbility(template);
            asc.TryActivateAbility(handle);
            return (MontageHostAbility)asc.FindAbilitySpec(handle).Ability;
        }

        // ============ A) duration 后 OnCompleted ============
        [UnityTest]
        public IEnumerator A_CompletesAfterDuration()
        {
            var ability = ActivateHost(out _);
            int completed = 0, cancelled = 0;
            var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(ability, null, null, 0.15f);
            t.OnCompleted += () => completed++;
            t.OnCancelled += () => cancelled++;
            t.Activate();

            Assert.AreEqual(0, completed, "刚激活未到时");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, completed, "duration 后应 OnCompleted 一次");
            Assert.AreEqual(0, cancelled, "正常完成不应 OnCancelled");
        }

        // ============ B) 匹配事件 OnEventReceived（多次/忽略不匹配）============
        [UnityTest]
        public IEnumerator B_EventReceived_MatchesAndIgnores()
        {
            var ability = ActivateHost(out var asc);
            var tags = new GameplayTagContainer(); tags.AddTag(Tag("Combat.HitConfirm"));
            int events = 0;
            var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(ability, null, null, 1f, tags);
            t.OnEventReceived += (tag, d) => events++;
            t.Activate();
            yield return null;

            asc.SendGameplayEvent(Tag("Combat.HitConfirm"));
            Assert.AreEqual(1, events, "匹配事件应触发");
            asc.SendGameplayEvent(Tag("Combat.Whiff"));
            Assert.AreEqual(1, events, "不匹配不触发");
            asc.SendGameplayEvent(Tag("Combat.HitConfirm"));
            Assert.AreEqual(2, events, "播放期间可多次触发");
        }

        // ============ C) OnBlendOut 早于 OnCompleted ============
        [UnityTest]
        public IEnumerator C_BlendOut_BeforeCompleted()
        {
            var ability = ActivateHost(out _);
            int blendOut = 0, completed = 0;
            bool blendBeforeComplete = false;
            var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(
                ability, null, null, 0.3f, null, 1f, 0.15f); // duration 0.3, blendOutTime 0.15
            t.OnBlendOut += () => { blendOut++; if (completed == 0) blendBeforeComplete = true; };
            t.OnCompleted += () => completed++;
            t.Activate();

            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(1, blendOut, "应 OnBlendOut 一次");
            Assert.AreEqual(1, completed, "应 OnCompleted 一次");
            Assert.IsTrue(blendBeforeComplete, "OnBlendOut 应早于 OnCompleted");
        }

        // ============ D) 技能结束 → OnCancelled、不再 OnCompleted ============
        [UnityTest]
        public IEnumerator D_EndAbility_Cancels()
        {
            var ability = ActivateHost(out _);
            int completed = 0, cancelled = 0;
            var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(ability, null, null, 0.5f);
            t.OnCompleted += () => completed++;
            t.OnCancelled += () => cancelled++;
            t.Activate();
            yield return null;

            ability.EndAbility(); // 统一 ExternalCancel 未结束任务 → OnCancelled
            Assert.AreEqual(1, cancelled, "技能结束应 OnCancelled 一次");

            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0, completed, "取消后不应再 OnCompleted");
        }

        // ============ E) 无效输入立即 OnCancelled ============
        [UnityTest]
        public IEnumerator E_InvalidInput_CancelsImmediately()
        {
            var ability = ActivateHost(out _);
            int cancelled = 0, completed = 0;
            var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(ability, null, null, 0f);
            t.OnCancelled += () => cancelled++;
            t.OnCompleted += () => completed++;
            t.Activate();

            Assert.AreEqual(1, cancelled, "无 animator 且 duration<=0 应立即 OnCancelled");
            Assert.AreEqual(0, completed);
            yield return null;
        }
    }
}
