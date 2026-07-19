// PlayMode 测试：ASC 生命周期清理 / 抑制摘挂标签 / 全局效果 level 补发 —— P1 批次2 回归。
//  A1) OnDestroy 回收授予的技能克隆（HideAndDontSave，否则真机泄漏）；
//  A4) 抑制翻转时同步摘/挂 GrantedTags（否则标签与属性表现矛盾）；
//  C4) GlobalAbilitySystem 对晚注册的 ASC 按原 level 补发效果（否则一律按 1 级错算）。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 最小可实例化技能（不做事），供 A1 授予/销毁测试。
    public class NoopAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) { }
    }

    public class AscLifecyclePlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            GlobalAbilitySystem.Instance.Clear(); // 单例隔离
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private GameObject NewGo(string n) { var go = new GameObject(n); _spawned.Add(go); return go; }
        private T NewAsset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }

        // ============ A1 ============
        [UnityTest]
        public IEnumerator OnDestroy_DestroysGrantedAbilityClones()
        {
            var go = NewGo("Caster");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var tmpl = NewAsset<NoopAbility>();
            var handle = asc.GiveAbility(tmpl);
            var clone = asc.FindAbilitySpec(handle).Ability;
            Assert.IsTrue(clone != null, "授予后应有克隆实例");
            Assert.AreNotSame(tmpl, clone, "应是克隆而非模板");

            Object.Destroy(go);
            yield return null;
            yield return null; // 等销毁与级联 Destroy 生效

            Assert.IsTrue(clone == null, "ASC 销毁后技能克隆应被回收（Unity fake-null）");
            Assert.IsTrue(tmpl != null, "模板不应被销毁");
        }

        // ============ A4 ============
        [UnityTest]
        public IEnumerator Inhibition_TogglesGrantedTags()
        {
            var go = NewGo("Target");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());

            // 定身效果：需要 State.Combat 才生效（OngoingRequiredTags），生效期间授予 State.Rooted。
            var root = NewAsset<GameplayEffect>();
            root.DurationType = EGameplayEffectDurationType.Infinite;
            root.OngoingRequiredTags.Add(Tag("State.Combat"));
            root.GrantedTags.Add(Tag("State.Rooted"));

            // 施加时不满足条件 → 一开始就被抑制，不应挂 Rooted
            asc.ApplyGameplayEffectToSelf(root);
            Assert.IsFalse(asc.HasMatchingGameplayTag(Tag("State.Rooted")), "被抑制时不应授予 State.Rooted");

            // 满足条件 → 下一帧 UpdateInhibition 解除抑制 → 挂 Rooted
            asc.AddLooseGameplayTag(Tag("State.Combat"));
            yield return null;
            Assert.IsTrue(asc.HasMatchingGameplayTag(Tag("State.Rooted")), "解除抑制后应授予 State.Rooted");

            // 再次不满足 → 下一帧重新抑制 → 摘 Rooted
            asc.RemoveLooseGameplayTag(Tag("State.Combat"));
            yield return null;
            Assert.IsFalse(asc.HasMatchingGameplayTag(Tag("State.Rooted")), "重新抑制后应摘除 State.Rooted");
        }

        // ============ C4 ============
        [UnityTest]
        public IEnumerator GlobalEffect_AppliesOriginalLevel_ToLateRegistrant()
        {
            var gas = GlobalAbilitySystem.Instance;
            gas.Clear();

            // MaxHealth += ScalableFloat(base=0, perLevel=10) → level 5 时 = +40。
            var buff = NewAsset<GameplayEffect>();
            buff.DurationType = EGameplayEffectDurationType.Infinite;
            buff.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().MaxHealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(0f, 10f)
            });

            gas.ApplyEffectToAll(buff, 5);

            var go = NewGo("LateRegistrant");
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());
            float before = asc.GetAttributeSet<AS_Health>().MaxHealth.CurrentValue;

            gas.RegisterASC(asc); // 晚注册 → 按记录的 level 5 补发

            float after = asc.GetAttributeSet<AS_Health>().MaxHealth.CurrentValue;
            Assert.AreEqual(before + 40f, after, 0.01f,
                "晚注册 ASC 应按原 level 5 补发（+40）；修复前一律按 level 1（+0）");
            yield return null;
        }
    }
}
