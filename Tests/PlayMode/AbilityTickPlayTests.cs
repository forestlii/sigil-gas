// PlayMode 测试：#16 技能级 AbilityTick（由 ASC.Update 驱动，仅 EnableTick=true 的激活技能）。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 测试技能：激活后保持，每帧累加 AbilityTick 的 dt 与次数。
    public class TickingAbility : GameplayAbility
    {
        public int TickCount;
        public float Accumulated;
        protected override void OnActivateAbility(GameplayEventData triggerData) { /* 保持激活 */ }
        public override void AbilityTick(float deltaTime) { TickCount++; Accumulated += deltaTime; }
    }

    public class AbilityTickPlayTests
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

        [UnityTest]
        public IEnumerator EnableTick_DrivesAbilityTickWhileActive()
        {
            var go = NewGo("Caster");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var tmpl = NewAsset<TickingAbility>();
            tmpl.EnableTick = true;
            var handle = asc.GiveAbility(tmpl);
            var ability = (TickingAbility)asc.FindAbilitySpec(handle).Ability;

            Assert.IsTrue(asc.TryActivateAbility(handle), "应能激活");
            Assert.AreEqual(0, ability.TickCount, "激活当帧尚未 Update");

            yield return null;
            yield return null;
            yield return null;

            Assert.GreaterOrEqual(ability.TickCount, 2, "激活期间应每帧回调 AbilityTick");
            Assert.Greater(ability.Accumulated, 0f, "应累计到正的 deltaTime");

            // 结束后不再 tick
            ability.EndAbility();
            int countAtEnd = ability.TickCount;
            yield return null;
            yield return null;
            Assert.AreEqual(countAtEnd, ability.TickCount, "技能结束后不应再 tick");
        }

        [UnityTest]
        public IEnumerator EnableTickFalse_NotTicked()
        {
            var go = NewGo("Caster");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var tmpl = NewAsset<TickingAbility>();
            tmpl.EnableTick = false; // 默认关
            var handle = asc.GiveAbility(tmpl);
            var ability = (TickingAbility)asc.FindAbilitySpec(handle).Ability;
            asc.TryActivateAbility(handle);

            yield return null;
            yield return null;

            Assert.AreEqual(0, ability.TickCount, "EnableTick=false 的技能不应被 tick");
        }
    }
}
