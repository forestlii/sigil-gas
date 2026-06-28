// PlayMode 测试：AbilityTask 框架（阶段1 补强）。
//  A) WaitGameplayEvent：匹配事件触发回调、不匹配不触发。
//  B) WaitDelay / WaitDelayOneFrame：计时/一帧后回调。
//  C) 技能结束清理：EndAbility 后任务被取消，事件不再回调（防悬挂）。
//  D) WaitInputPress：输入按下 + 状态标签门控。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 测试技能：激活时挂一个 WaitGameplayEvent（持续监听）+ 一个 WaitDelay。
    public class TaskHostAbility : GameplayAbility
    {
        public int EventFired;
        public int DelayFired;
        public GameplayTag LastEventTag;

        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            var evt = AbilityTask_WaitGameplayEvent.WaitGameplayEvent(
                this, GameplayTag.RequestTag("Combat.HitConfirm"), onlyTriggerOnce: false);
            evt.OnEventReceived += (t, d) => { EventFired++; LastEventTag = t; };
            evt.Activate();

            var delay = AbilityTask_WaitDelay.WaitDelay(this, 0.15f);
            delay.OnFinish += () => DelayFired++;
            delay.Activate();
            // 保持激活，由测试显式结束
        }
    }

    // 测试技能：激活时挂一个 WaitInputPress（需 Movement.State.Sprint，禁 State.Stunned）。
    public class InputWaitAbility : GameplayAbility
    {
        public int Pressed;
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            var req = new GameplayTagContainer(); req.AddTag(GameplayTag.RequestTag("Movement.State.Sprint"));
            var ign = new GameplayTagContainer(); ign.AddTag(GameplayTag.RequestTag("State.Stunned"));
            var t = AbilityTask_WaitInputPress.WaitInputPress(
                this, GameplayTag.RequestTag("InputTag.Attack"), req, ign,
                InputTriggerEvent.Started, onlyTriggerOnce: false);
            t.OnPress += _ => Pressed++;
            t.Activate();
        }
    }

    public class AbilityTaskPlayTests
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

        private GameObject NewGo(string name) { var go = new GameObject(name); _spawned.Add(go); return go; }
        private T NewAsset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }

        // 造一个角色 + ASC，授予 TaskHostAbility 并激活，返回实例
        private TaskHostAbility ActivateHost(out AbilitySystemComponent asc)
        {
            var go = NewGo("Caster");
            asc = go.AddComponent<AbilitySystemComponent>();
            var template = NewAsset<TaskHostAbility>();
            template.AbilityTags.Add(Tag("Ability.TaskHost"));
            var handle = asc.GiveAbility(template);
            asc.TryActivateAbility(handle);
            // 取激活后的实例（GiveAbility 克隆了模板）
            return (TaskHostAbility)asc.FindAbilitySpec(handle).Ability;
        }

        // ============ A) WaitGameplayEvent 匹配/不匹配 ============
        [UnityTest]
        public IEnumerator A_WaitGameplayEvent_MatchesAndIgnores()
        {
            var ability = ActivateHost(out var asc);
            yield return null;

            asc.SendGameplayEvent(Tag("Combat.HitConfirm"));
            Assert.AreEqual(1, ability.EventFired, "匹配事件应触发一次");
            Assert.AreEqual("Combat.HitConfirm", ability.LastEventTag.TagName);

            asc.SendGameplayEvent(Tag("Combat.Whiff")); // 不匹配
            Assert.AreEqual(1, ability.EventFired, "不匹配事件不应触发");

            asc.SendGameplayEvent(Tag("Combat.HitConfirm")); // 持续监听，可再次触发
            Assert.AreEqual(2, ability.EventFired, "持续监听应可再次触发");
        }

        // ============ B) WaitDelay / WaitDelayOneFrame ============
        [UnityTest]
        public IEnumerator B_WaitDelay_FiresAfterDuration()
        {
            var ability = ActivateHost(out _);
            Assert.AreEqual(0, ability.DelayFired, "刚激活时延时未到");
            yield return new WaitForSeconds(0.25f); // > 0.15s
            Assert.AreEqual(1, ability.DelayFired, "延时到点应回调一次");
        }

        [UnityTest]
        public IEnumerator B_WaitDelayOneFrame_FiresNextFrame()
        {
            var go = NewGo("Caster");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var template = NewAsset<TaskHostAbility>();
            var handle = asc.GiveAbility(template);
            asc.TryActivateAbility(handle);
            var ability = (TaskHostAbility)asc.FindAbilitySpec(handle).Ability;

            int fired = 0;
            var oneFrame = AbilityTask_WaitDelayOneFrame.WaitOneFrame(ability);
            oneFrame.OnFinish += () => fired++;
            oneFrame.Activate();

            Assert.AreEqual(0, fired, "同帧未到");
            yield return null;
            yield return null;
            Assert.AreEqual(1, fired, "下一帧应回调");
        }

        // ============ C) 技能结束清理：任务被取消，事件不再回调 ============
        [UnityTest]
        public IEnumerator C_EndAbility_CancelsTasks()
        {
            var ability = ActivateHost(out var asc);
            yield return null;

            asc.SendGameplayEvent(Tag("Combat.HitConfirm"));
            Assert.AreEqual(1, ability.EventFired);

            ability.EndAbility(); // 应取消所有未结束任务并解绑事件

            asc.SendGameplayEvent(Tag("Combat.HitConfirm"));
            Assert.AreEqual(1, ability.EventFired, "技能结束后事件任务应已解绑，不再回调");

            // 结束前挂的 WaitDelay 也应被取消：再等也不增加
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(0, ability.DelayFired, "技能结束后延时任务应被取消");
        }

        // ============ D) WaitInputPress + 状态门控 ============
        [UnityTest]
        public IEnumerator D_WaitInputPress_GatedByTags()
        {
            var go = NewGo("Caster");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var ic = go.AddComponent<InputSystemComponent>();
            var template = NewAsset<InputWaitAbility>();
            var handle = asc.GiveAbility(template);
            yield return null; // InputSystemComponent.Awake 接上 ASC

            asc.TryActivateAbility(handle);
            var ability = (InputWaitAbility)asc.FindAbilitySpec(handle).Ability;

            // 无 Sprint → 门控不通过
            ic.ReceiveInput(Tag("InputTag.Attack"), InputTriggerEvent.Started, InputActionData.Empty);
            Assert.AreEqual(0, ability.Pressed, "缺 RequiredTags(Sprint) 时输入应被门控忽略");

            // 加 Sprint → 通过
            asc.AddLooseGameplayTag(Tag("Movement.State.Sprint"));
            ic.ReceiveInput(Tag("InputTag.Attack"), InputTriggerEvent.Started, InputActionData.Empty);
            Assert.AreEqual(1, ability.Pressed, "满足 Sprint 时输入应触发");

            // 加 Stunned（IgnoredTags）→ 又被门控
            asc.AddLooseGameplayTag(Tag("State.Stunned"));
            ic.ReceiveInput(Tag("InputTag.Attack"), InputTriggerEvent.Started, InputActionData.Empty);
            Assert.AreEqual(1, ability.Pressed, "含 IgnoredTags(Stunned) 时输入应被忽略");
        }
    }
}
