// PlayMode 测试：表现层运行时（GameplayCue 路由、上下文特效组件、相机模式栈混合）。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 测试用 cue 处理器：记录被调用次数与事件类型
    public class TestCueNotify : GameplayCueNotify
    {
        public int Count;
        public EGameplayCueEvent LastEvent;
        public override void HandleCue(GameObject target, EGameplayCueEvent cueEvent, GameplayCueParameters p)
        {
            Count++; LastEvent = cueEvent;
        }
    }

    public class PresentationPlayTests
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
            GameplayCueManager.Instance.Clear();
        }

        private GameObject NewGo(string n) { var g = new GameObject(n); _spawned.Add(g); return g; }
        private T NewAsset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }

        // A) 效果施加触发 GameplayCue（Executed），且子标签路由到父标签处理器
        [UnityTest]
        public IEnumerator A_GameplayCue_RoutedFromEffect()
        {
            var go = NewGo("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());

            // 父标签处理器，应被子标签 cue 命中
            var notify = NewAsset<TestCueNotify>();
            notify.CueTag = Tag("GameplayCue.Hit");
            GameplayCueManager.Instance.RegisterCueNotify(notify);

            // 瞬时伤害效果，带子标签 cue
            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("IncomingDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(10f)
            });
            ge.GameplayCues.Add(Tag("GameplayCue.Hit.Flesh"));

            asc.ApplyGameplayEffectToSelf(ge);
            yield return null;

            Assert.AreEqual(1, notify.Count, "子标签 cue 应路由到父标签处理器");
            Assert.AreEqual(EGameplayCueEvent.Executed, notify.LastEvent, "瞬时效果应为 Executed 事件");
        }

        // B) 持续效果施加→OnActive，移除→Removed
        [UnityTest]
        public IEnumerator B_GameplayCue_ActiveAndRemoved()
        {
            var go = NewGo("ASC");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());

            var events = new List<EGameplayCueEvent>();
            System.Action<GameObject, GameplayTag, EGameplayCueEvent, GameplayCueParameters> h =
                (t, tag, ev, p) => { if (tag == Tag("GameplayCue.Buff")) events.Add(ev); };
            GameplayCueManager.Instance.OnGameplayCue += h;

            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Infinite;
            ge.GameplayCues.Add(Tag("GameplayCue.Buff"));

            var handle = asc.ApplyGameplayEffectToSelf(ge);
            asc.RemoveActiveGameplayEffect(handle);
            GameplayCueManager.Instance.OnGameplayCue -= h;
            yield return null;

            Assert.Contains(EGameplayCueEvent.OnActive, events, "施加持续效果应触发 OnActive");
            Assert.Contains(EGameplayCueEvent.Removed, events, "移除持续效果应触发 Removed");
        }

        // C) 上下文特效组件：按表面聚合情景并触发
        [UnityTest]
        public IEnumerator C_ContextEffect_AggregatesSurface()
        {
            var go = NewGo("FX");
            var cec = go.AddComponent<SurfaceEffectComponent>();
            var lib = NewAsset<SurfaceEffectLibrary>();
            lib.Entries.Add(new SurfaceEffectEntry
            {
                EffectTag = Tag("ContextEffect.Footstep"),
                Contexts = new List<GameplayTag> { Tag("SurfaceType.Grass") }
            });
            cec.AddLibrary(lib);

            GameplayTag gotEffect = default;
            GameplayTagContainer gotContexts = null;
            cec.OnSurfaceEffect += (eff, ctx, loc) => { gotEffect = eff; gotContexts = ctx; };

            cec.PlaySurfaceEffect(Tag("ContextEffect.Footstep"), Vector3.zero, Tag("SurfaceType.Grass"));
            yield return null;

            Assert.AreEqual(Tag("ContextEffect.Footstep"), gotEffect);
            Assert.IsNotNull(gotContexts);
            Assert.IsTrue(gotContexts.HasTag(Tag("SurfaceType.Grass")), "聚合情景应含表面标签");
        }

        // D) 相机模式栈：混合权重随时间爬升，栈顶满权后裁剪下层（显式 dt，绕开 headless 时序）
        [UnityTest]
        public IEnumerator D_CameraStack_BlendsAndPrunes()
        {
            var target = NewGo("CamTarget").transform;
            var stack = new CameraBlendStack();

            var modeA = new ThirdPersonCameraBehavior { BlendInDuration = 0f };  // 立即生效
            var modeB = new ThirdPersonCameraBehavior { BlendInDuration = 1f };  // 1 秒混入

            stack.Push(modeA);
            stack.Evaluate(target, 0.1f);
            Assert.AreEqual(1, stack.Count);

            stack.Push(modeB);
            stack.Evaluate(target, 0.5f); // modeB 混入一半
            stack.GetTopBlend(out float w, out _);
            Assert.Greater(w, 0.05f, "栈顶应正在混入");
            Assert.Less(w, 1f, "尚未混入完成");
            Assert.AreEqual(2, stack.Count, "混合期间两层共存");

            stack.Evaluate(target, 1f); // modeB 混入完成
            stack.GetTopBlend(out float w2, out _);
            Assert.AreEqual(1f, w2, 0.01f, "栈顶应满权重");
            Assert.AreEqual(1, stack.Count, "栈顶满权后应裁掉下层");
            yield return null;
        }
    }
}
