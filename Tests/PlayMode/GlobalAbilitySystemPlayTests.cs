// PlayMode 测试：GlobalAbilitySystem（全局给所有注册 ASC 施加技能/效果）。
// 放 PlayMode 而非 EditMode：ClearAbility 内部用 Object.Destroy，EditMode 禁用、PlayMode 正常。
//  A) ApplyAbilityToAll 给已注册 ASC；B) 后注册 ASC 自动获得；C) RemoveAbilityFromAll 撤销；
//  D) ApplyEffectToAll 改所有 ASC 属性 + RemoveEffectFromAll 回原值；E) UnregisterASC 撤销并隔离后续。
// 单例 → 每个测试 SetUp/TearDown Clear() 重置，避免状态串测。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 激活即结束的测试技能（验证 ASC 是否被授予全局技能）
    public class GlobalTestAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData) => EndAbility();
    }

    public class GlobalAbilitySystemPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void Reset() => GlobalAbilitySystem.Instance.Clear();

        [TearDown]
        public void Cleanup()
        {
            GlobalAbilitySystem.Instance.Clear();
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear(); _assets.Clear();
        }

        private AbilitySystemComponent NewRegisteredASC()
        {
            var go = new GameObject("ASC"); _spawned.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());
            GlobalAbilitySystem.Instance.RegisterASC(asc);
            return asc;
        }

        private GlobalTestAbility NewAbilityAsset(string tag)
        {
            var a = ScriptableObject.CreateInstance<GlobalTestAbility>(); _assets.Add(a);
            a.AbilityTags.Add(Tag(tag));
            return a;
        }

        private GameplayEffect NewMaxHealthBuff(float amount)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.Infinite;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().MaxHealthAttribute, // 按 (集合类型, 属性名) 定位，不绑该临时实例
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(amount)
            });
            return ge;
        }

        // ============ A) Apply 给已注册 ASC ============
        [UnityTest]
        public IEnumerator A_ApplyAbilityToAll_GivesToRegistered()
        {
            var asc1 = NewRegisteredASC();
            var asc2 = NewRegisteredASC();
            GlobalAbilitySystem.Instance.ApplyAbilityToAll(NewAbilityAsset("Ability.GlobalBuff"));
            Assert.IsTrue(asc1.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "asc1 应获得全局技能");
            Assert.IsTrue(asc2.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "asc2 应获得全局技能");
            yield return null;
        }

        // ============ B) 后注册 ASC 自动获得 ============
        [UnityTest]
        public IEnumerator B_LateRegistered_GetsGlobalAbility()
        {
            NewRegisteredASC();
            GlobalAbilitySystem.Instance.ApplyAbilityToAll(NewAbilityAsset("Ability.GlobalBuff"));
            var late = NewRegisteredASC(); // 全局应用之后才注册
            Assert.IsTrue(late.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "后注册 ASC 应自动补上已全局应用的技能");
            yield return null;
        }

        // ============ C) RemoveAbilityFromAll ============
        [UnityTest]
        public IEnumerator C_RemoveAbilityFromAll()
        {
            var asc1 = NewRegisteredASC();
            var ability = NewAbilityAsset("Ability.GlobalBuff");
            GlobalAbilitySystem.Instance.ApplyAbilityToAll(ability);
            GlobalAbilitySystem.Instance.RemoveAbilityFromAll(ability);
            Assert.IsFalse(asc1.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "移除后 ASC 不应再有该技能");
            yield return null;
        }

        // ============ D) Effect 施加/移除 ============
        [UnityTest]
        public IEnumerator D_ApplyAndRemoveEffectToAll()
        {
            var asc1 = NewRegisteredASC();
            var asc2 = NewRegisteredASC();
            var h1 = asc1.GetAttributeSet<AS_Health>();
            var h2 = asc2.GetAttributeSet<AS_Health>();
            float before = h1.MaxHealth.CurrentValue;

            var buff = NewMaxHealthBuff(50f);
            GlobalAbilitySystem.Instance.ApplyEffectToAll(buff);
            Assert.AreEqual(before + 50f, h1.MaxHealth.CurrentValue, 0.01f, "asc1 MaxHealth +50");
            Assert.AreEqual(before + 50f, h2.MaxHealth.CurrentValue, 0.01f, "asc2 MaxHealth +50");

            GlobalAbilitySystem.Instance.RemoveEffectFromAll(buff);
            Assert.AreEqual(before, h1.MaxHealth.CurrentValue, 0.01f, "移除后 asc1 回原值");
            Assert.AreEqual(before, h2.MaxHealth.CurrentValue, 0.01f, "移除后 asc2 回原值");
            yield return null;
        }

        // ============ E) Unregister 撤销 + 隔离后续 ============
        [UnityTest]
        public IEnumerator E_UnregisterASC_RemovesAndIsolates()
        {
            var asc1 = NewRegisteredASC();
            var asc2 = NewRegisteredASC();
            GlobalAbilitySystem.Instance.ApplyAbilityToAll(NewAbilityAsset("Ability.GlobalBuff"));

            GlobalAbilitySystem.Instance.UnregisterASC(asc1);
            Assert.IsFalse(asc1.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "注销后 asc1 失去全局技能");
            Assert.IsTrue(asc2.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff")), "asc2 不受影响");

            GlobalAbilitySystem.Instance.ApplyAbilityToAll(NewAbilityAsset("Ability.GlobalBuff2"));
            Assert.IsFalse(asc1.TryActivateAbilitiesByTag(Tag("Ability.GlobalBuff2")), "注销后不再收到新全局技能");
            yield return null;
        }
    }
}
