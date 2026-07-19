// PlayMode 测试：周期效果（DoT / 回血）不双重计数 —— P0-1 回归。
// 修复前：周期效果的 modifier 既作为持续修饰被聚合进 CurrentValue、又每周期落 BaseValue，
// 同一 magnitude 被算两遍——-10 HP/s 的 DoT 施加瞬间 CurrentValue 就掉 10，
// 第一个 tick 后 BaseValue=90 而 CurrentValue=80，血条整段读数低一个 tick。
// 放 PlayMode：依赖 ASC.Update 推进周期结算。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class PeriodicEffectPlayTests
    {
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
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());
            return asc;
        }

        // HasDuration + Period 的周期效果：每 period 秒对 Health 施加 perTick（Add）。
        private GameplayEffect NewPeriodicHealthEffect(float perTick, float period, float duration)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.HasDuration;
            ge.Duration = duration;
            ge.Period = period;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().HealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(perTick)
            });
            return ge;
        }

        [UnityTest]
        public IEnumerator DoT_DoesNotDoubleCount_OnApplyAndPerTick()
        {
            var asc = NewASC();
            var h = asc.GetAttributeSet<AS_Health>();
            float start = h.Health.CurrentValue; // 100

            var dot = NewPeriodicHealthEffect(-10f, 0.1f, 10f);
            asc.ApplyGameplayEffectToSelf(dot);

            // 施加瞬间（尚未结算周期）：周期效果不参与 CurrentValue 聚合 → 血条不应立即掉。
            // 修复前这里会立即变成 start-10（持续修饰被误聚合）。
            Assert.AreEqual(start, h.Health.CurrentValue, 0.001f, "周期效果施加瞬间不应改变 CurrentValue");
            Assert.AreEqual(start, h.Health.BaseValue, 0.001f, "施加瞬间 BaseValue 也不变");

            // 等到恰好结算一个周期（0.1 < 0.16 < 0.2 → 累计 dt 只够 1 个 tick）。
            yield return new WaitForSeconds(0.16f);

            // 关键不变式：周期效果只落 BaseValue，CurrentValue 应恒等于 BaseValue（无幽灵持续修饰）。
            Assert.AreEqual(h.Health.BaseValue, h.Health.CurrentValue, 0.001f,
                "周期效果不该在 BaseValue 之外再叠一份持续修饰");
            // 一个 tick 后应是 start-10；修复前 CurrentValue 会是 start-20（双重计数）。
            Assert.AreEqual(start - 10f, h.Health.BaseValue, 0.001f, "一个周期后 BaseValue 应为 start-10");
            Assert.AreEqual(start - 10f, h.Health.CurrentValue, 0.001f, "一个周期后 CurrentValue 应为 start-10（非 start-20）");
        }
    }
}
