// EditMode 测试：有状态 Cue（GameplayCueNotify_Actor）——OnActive spawn / WhileActive 转发 / Removed 销毁 / 实例复用。
using NUnit.Framework;
using UnityEngine;
using Likeon.GAS;

namespace Likeon.GAS.Tests
{
    // 记录各生命周期事件调用的测试用有状态 Cue。
    public class TestCueNotify_Actor : GameplayCueNotify_Actor
    {
        public int OnActiveCount, WhileActiveCount, OnRemoveCount;
        public GameObject SeenTarget, SeenSpawned;

        public override void OnActive(GameObject target, GameObject spawned, GameplayCueParameters p)
        { OnActiveCount++; SeenTarget = target; SeenSpawned = spawned; }
        public override void WhileActive(GameObject target, GameObject spawned, GameplayCueParameters p, float dt)
        { WhileActiveCount++; }
        public override void OnRemove(GameObject target, GameObject spawned, GameplayCueParameters p)
        { OnRemoveCount++; }
    }

    public class GameplayCueNotifyActorEditTests
    {
        private const string CueTagName = "GameplayCue.Buff.Aura";

        [SetUp]
        public void SetUp() => GameplayCueManager.Instance.Clear();

        [TearDown]
        public void TearDown() => GameplayCueManager.Instance.Clear();

        private static TestCueNotify_Actor MakeNotify()
        {
            var notify = ScriptableObject.CreateInstance<TestCueNotify_Actor>();
            notify.CueTag = GameplayTag.RequestTag(CueTagName);
            GameplayCueManager.Instance.RegisterCueNotify(notify);
            return notify;
        }

        [Test]
        public void OnActive_SpawnsInstance_AndCallsOnActive()
        {
            var notify = MakeNotify();
            var target = new GameObject("Target");

            GameplayCueManager.Instance.HandleGameplayCue(target, GameplayTag.RequestTag(CueTagName), EGameplayCueEvent.OnActive, null);

            Assert.AreEqual(1, notify.OnActiveCount, "OnActive 应回调一次");
            Assert.AreEqual(1, GameplayCueManager.Instance.ActiveActorCueCount, "应登记一个活跃实例");
            Assert.IsTrue(GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var inst));
            Assert.AreSame(target, notify.SeenTarget, "OnActive 应收到 target");

            Object.DestroyImmediate(inst.gameObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void Removed_CallsOnRemove_AndClearsInstance()
        {
            var notify = MakeNotify();
            var target = new GameObject("Target");
            var tag = GameplayTag.RequestTag(CueTagName);

            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var inst);
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.Removed, null);

            Assert.AreEqual(1, notify.OnRemoveCount, "Removed 应回调 OnRemove");
            Assert.AreEqual(0, GameplayCueManager.Instance.ActiveActorCueCount, "移除后不应再有活跃实例");

            if (inst != null) Object.DestroyImmediate(inst.gameObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void RepeatedOnActive_DoesNotDoubleSpawn()
        {
            var notify = MakeNotify();
            var target = new GameObject("Target");
            var tag = GameplayTag.RequestTag(CueTagName);

            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);

            Assert.AreEqual(1, notify.OnActiveCount, "同 target 同 Cue 重复 OnActive 不应重复 spawn");
            Assert.AreEqual(1, GameplayCueManager.Instance.ActiveActorCueCount);

            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var inst);
            if (inst != null) Object.DestroyImmediate(inst.gameObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void WhileActive_ForwardsToNotify()
        {
            var notify = MakeNotify();
            var target = new GameObject("Target");
            var tag = GameplayTag.RequestTag(CueTagName);

            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.WhileActive, null);
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.WhileActive, null);

            Assert.AreEqual(2, notify.WhileActiveCount, "WhileActive 应转发给实例两次");

            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var inst);
            if (inst != null) Object.DestroyImmediate(inst.gameObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void SpawnPrefab_IsInstantiated_AndAttachedToTarget()
        {
            var notify = MakeNotify();
            notify.SpawnPrefab = new GameObject("PrefabTemplate");
            notify.AttachToTarget = true;
            var target = new GameObject("Target");

            GameplayCueManager.Instance.HandleGameplayCue(target, GameplayTag.RequestTag(CueTagName), EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var inst);

            Assert.IsNotNull(inst.Spawned, "应实例化 SpawnPrefab");
            Assert.AreSame(target.transform, inst.transform.parent, "AttachToTarget 时宿主应挂到 target 下");

            Object.DestroyImmediate(inst.gameObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify.SpawnPrefab);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void StaticCue_StillDispatches_NoRegression()
        {
            // 回归：无状态 _Static 走原路径，不受有状态实例表影响
            var notify = ScriptableObject.CreateInstance<GameplayCueNotify_Static>();
            notify.CueTag = GameplayTag.RequestTag("GameplayCue.Hit");
            GameplayCueManager.Instance.RegisterCueNotify(notify);

            bool broadcast = false;
            System.Action<GameObject, GameplayTag, EGameplayCueEvent, GameplayCueParameters> h =
                (t, tag, ev, p) => broadcast = true;
            GameplayCueManager.Instance.OnGameplayCue += h;

            var target = new GameObject("Target");
            GameplayCueManager.Instance.HandleGameplayCue(target, GameplayTag.RequestTag("GameplayCue.Hit"), EGameplayCueEvent.Executed, null);

            Assert.IsTrue(broadcast, "_Static Cue 事件仍应广播（无回归）");
            Assert.AreEqual(0, GameplayCueManager.Instance.ActiveActorCueCount, "_Static 不应产生有状态实例");

            GameplayCueManager.Instance.OnGameplayCue -= h;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify);
        }

        [Test]
        public void RemovedActor_IsPooled_AndReusedOnNextActivation()
        {
            var notify = MakeNotify();
            var target = new GameObject("Target");
            var tag = GameplayTag.RequestTag(CueTagName);

            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var first);
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.Removed, null);

            Assert.AreEqual(0, GameplayCueManager.Instance.ActiveActorCueCount, "移除后不再活跃");
            Assert.AreEqual(1, GameplayCueManager.Instance.PooledActorCount, "移除的实例应回池、而非销毁");

            // 再次激活 → 复用池中实例（不新建）
            GameplayCueManager.Instance.HandleGameplayCue(target, tag, EGameplayCueEvent.OnActive, null);
            GameplayCueManager.Instance.TryGetActorCueInstance(notify, target, out var second);

            Assert.AreSame(first, second, "再次激活应复用回池的同一实例");
            Assert.AreEqual(0, GameplayCueManager.Instance.PooledActorCount, "复用后池应清空");
            Assert.AreEqual(2, notify.OnActiveCount, "复用也回调 OnActive（共两次）");
            Assert.AreEqual(1, notify.OnRemoveCount, "OnRemove 一次");

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(notify); // 池中/活跃实例由 TearDown 的 Clear 收尾
        }
    }
}
