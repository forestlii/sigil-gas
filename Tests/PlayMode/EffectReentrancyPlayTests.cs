// PlayMode 测试：效果结算的重入安全（作用域锁 + 延迟队列）—— A2/A3 回归。
//  A2) 结算/属性钩子里请求移除效果 → 延迟到作用域结束再执行（不当场改列表 → 不跳 tick、不错乱）；
//  A3) 结算/属性钩子里无条件自施加效果 → 入队收敛而非递归爆栈（超限时报错并中止，不崩进程）。
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class EffectReentrancyPlayTests
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
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());
            return asc;
        }

        private GameplayEffect PeriodicHealth(float perTick, float period)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.HasDuration;
            ge.Duration = 100f; ge.Period = period;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().HealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(perTick)
            });
            return ge;
        }

        private GameplayEffect InfiniteTag(string tag)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.Infinite;
            ge.GrantedTags.Add(Tag(tag));
            return ge;
        }

        private GameplayEffect InstantMaxHealth(float delta)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().MaxHealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(delta)
            });
            return ge;
        }

        // ============ A2：tick 内请求移除 → 延迟到作用域结束 ============
        [UnityTest]
        public IEnumerator A2_RemovalDuringTick_IsDeferredThenApplied()
        {
            var asc = NewASC();
            asc.ApplyGameplayEffectToSelf(PeriodicHealth(-1f, 0.1f)); // 周期效果，tick 时会触发属性变化钩子
            var buffHandle = asc.ApplyGameplayEffectToSelf(InfiniteTag("State.Buff")); // 待移除的无限效果
            Assert.AreEqual(2, asc.GetActiveGameplayEffects().Count);

            bool handlerRan = false, stillPresentDuringScope = false;
            asc.OnAttributeChanged += _ =>
            {
                if (handlerRan) return;
                handlerRan = true;
                asc.RemoveActiveGameplayEffect(buffHandle);                       // 在 tick 作用域内请求移除
                stillPresentDuringScope = asc.GetActiveGameplayEffect(buffHandle) != null; // 当下应仍在（延迟）
            };

            yield return new WaitForSeconds(0.16f); // 让周期效果 tick 一次

            Assert.IsTrue(handlerRan, "周期 tick 应触发属性变化钩子");
            Assert.IsTrue(stillPresentDuringScope, "作用域内请求移除应被延迟——当场 buff 仍在");
            Assert.IsNull(asc.GetActiveGameplayEffect(buffHandle), "作用域结束后 buff 应已被移除（flush 生效）");
            Assert.IsFalse(asc.HasMatchingGameplayTag(Tag("State.Buff")), "buff 的授予标签也应随移除撤下");
        }

        // ============ A3：钩子内无条件自施加 → 收敛不爆栈 ============
        [UnityTest]
        public IEnumerator A3_ReentrantApply_IsCapped_NoStackOverflow()
        {
            var asc = NewASC();
            var grow = InstantMaxHealth(1f); // 每次 +1 MaxHealth（无上限 → 不收敛，用于逼出队列上限兜底）

            // 每次属性变化都再自施加一次 → 若当场递归执行会无限递归爆栈；有延迟队列则入队、超限报错中止。
            asc.OnAttributeChanged += _ => asc.ApplyGameplayEffectToSelf(grow);

            LogAssert.Expect(LogType.Error, new Regex("延迟队列超限"));
            Assert.DoesNotThrow(() => asc.ApplyGameplayEffectToSelf(grow), "重入自施加不应爆栈崩溃");

            // 到这里说明进程没崩、队列被上限兜住；MaxHealth 涨了有限次。
            Assert.Greater(asc.GetAttributeSet<AS_Health>().MaxHealth.CurrentValue, 100f, "确有若干次自施加生效");
            yield return null;
        }
    }
}
