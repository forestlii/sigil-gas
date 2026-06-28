// PlayMode 测试：GameplayEffect 叠层 stacking。
//  A) AggregateByTarget 合并加层 + 修改量按层放大；
//  B) 层数上限封顶；
//  C) OnActiveEffectStackChanged 携带正确旧/新层数；
//  D) 时长刷新策略（再施加刷新倒计时）；
//  E) 到期策略 RemoveSingleStackAndRefreshDuration（逐层衰减）；
//  F) StackingType=None 回归：每次施加各算各的独立实例。
// 放 PlayMode：依赖 ASC.Update 推进时长/周期。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class EffectStackingPlayTests
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

        // MaxHealth +perStack 的可叠层效果（每层加 perStack）
        private GameplayEffect NewStackBuff(
            float perStack,
            EGameplayEffectStackingType stacking,
            int limit,
            EGameplayEffectDurationType dur = EGameplayEffectDurationType.Infinite,
            float duration = 0f,
            EGameplayEffectStackingExpirationPolicy expiry = EGameplayEffectStackingExpirationPolicy.ClearEntireStack)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = dur;
            ge.Duration = duration;
            ge.StackingType = stacking;
            ge.StackLimitCount = limit;
            ge.StackExpirationPolicy = expiry;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().MaxHealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(perStack)
            });
            return ge;
        }

        // ============ A) 合并加层 + 数值按层放大 ============
        [UnityTest]
        public IEnumerator A_AggregateByTarget_MergesAndScales()
        {
            var asc = NewASC();
            var h = asc.GetAttributeSet<AS_Health>();
            float before = h.MaxHealth.CurrentValue;
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.AggregateByTarget, 0);

            var h1 = asc.ApplyGameplayEffectToSelf(buff);
            var h2 = asc.ApplyGameplayEffectToSelf(buff);

            Assert.IsTrue(h1.Equals(h2), "同组再次施加应返回同一句柄（合并）");
            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count, "应合并为 1 个激活实例");
            Assert.AreEqual(2, asc.GetActiveGameplayEffect(h1).StackCount, "层数应为 2");
            Assert.AreEqual(before + 20f, h.MaxHealth.CurrentValue, 0.01f, "+10 × 2 层 = +20");
            yield return null;
        }

        // ============ B) 层数上限封顶 ============
        [UnityTest]
        public IEnumerator B_StackLimit_CapsCount()
        {
            var asc = NewASC();
            var h = asc.GetAttributeSet<AS_Health>();
            float before = h.MaxHealth.CurrentValue;
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.AggregateByTarget, 3);

            ActiveGameplayEffectHandle handle = default;
            for (int i = 0; i < 5; i++) handle = asc.ApplyGameplayEffectToSelf(buff);

            Assert.AreEqual(3, asc.GetActiveGameplayEffect(handle).StackCount, "上限 3，封顶在 3 层");
            Assert.AreEqual(before + 30f, h.MaxHealth.CurrentValue, 0.01f, "封顶后 +10 × 3 = +30");
            yield return null;
        }

        // ============ C) 层变事件 old/new ============
        [UnityTest]
        public IEnumerator C_StackChanged_FiresWithCounts()
        {
            var asc = NewASC();
            var changes = new List<(int oldC, int newC)>();
            asc.OnActiveEffectStackChanged += (e, o, n) => changes.Add((o, n));
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.AggregateByTarget, 10);

            asc.ApplyGameplayEffectToSelf(buff); // 首次=Added，不触发 stack-change
            asc.ApplyGameplayEffectToSelf(buff); // 1→2
            asc.ApplyGameplayEffectToSelf(buff); // 2→3

            Assert.AreEqual(2, changes.Count, "应触发 2 次层变");
            Assert.AreEqual((1, 2), changes[0]);
            Assert.AreEqual((2, 3), changes[1]);
            yield return null;
        }

        // ============ D) 时长刷新 ============
        [UnityTest]
        public IEnumerator D_DurationRefresh_OnReapply()
        {
            var asc = NewASC();
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.AggregateByTarget, 0,
                EGameplayEffectDurationType.HasDuration, 1.0f);

            var handle = asc.ApplyGameplayEffectToSelf(buff);
            yield return new WaitForSeconds(0.4f);
            float beforeReapply = asc.GetActiveGameplayEffect(handle).TimeRemaining;
            Assert.Less(beforeReapply, 0.8f, "0.4s 后剩余应已下降");

            asc.ApplyGameplayEffectToSelf(buff); // 默认 RefreshOnSuccessfulApplication
            float afterReapply = asc.GetActiveGameplayEffect(handle).TimeRemaining;
            Assert.Greater(afterReapply, 0.9f, "再施加应把时长刷回接近满");
        }

        // ============ E) 到期掉一层 ============
        [UnityTest]
        public IEnumerator E_RemoveSingleStack_OnExpiry()
        {
            var asc = NewASC();
            var h = asc.GetAttributeSet<AS_Health>();
            float before = h.MaxHealth.CurrentValue;
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.AggregateByTarget, 0,
                EGameplayEffectDurationType.HasDuration, 0.1f,
                EGameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration);

            var handle = asc.ApplyGameplayEffectToSelf(buff);
            asc.ApplyGameplayEffectToSelf(buff); // 2 层
            Assert.AreEqual(2, asc.GetActiveGameplayEffect(handle).StackCount);

            yield return new WaitForSeconds(0.15f); // 第一次到期 → 掉到 1 层
            var afterOne = asc.GetActiveGameplayEffect(handle);
            Assert.IsNotNull(afterOne, "掉一层后效果仍在");
            Assert.AreEqual(1, afterOne.StackCount, "应掉到 1 层");
            Assert.AreEqual(before + 10f, h.MaxHealth.CurrentValue, 0.01f, "数值回到 1 层");

            yield return new WaitForSeconds(0.15f); // 最后一层到期 → 整体移除
            Assert.AreEqual(0, asc.GetActiveGameplayEffects().Count, "最后一层到期后整体移除");
            Assert.AreEqual(before, h.MaxHealth.CurrentValue, 0.01f, "数值回原值");
        }

        // ============ F) None 回归：不合并 ============
        [UnityTest]
        public IEnumerator F_StackingNone_IndependentInstances()
        {
            var asc = NewASC();
            var h = asc.GetAttributeSet<AS_Health>();
            float before = h.MaxHealth.CurrentValue;
            var buff = NewStackBuff(10f, EGameplayEffectStackingType.None, 0);

            var h1 = asc.ApplyGameplayEffectToSelf(buff);
            var h2 = asc.ApplyGameplayEffectToSelf(buff);

            Assert.IsFalse(h1.Equals(h2), "None 应产生不同句柄");
            Assert.AreEqual(2, asc.GetActiveGameplayEffects().Count, "应是 2 个独立实例");
            Assert.AreEqual(1, asc.GetActiveGameplayEffect(h1).StackCount, "独立实例层数恒为 1");
            Assert.AreEqual(before + 20f, h.MaxHealth.CurrentValue, 0.01f, "两份各 +10 = +20");
            yield return null;
        }
    }
}
