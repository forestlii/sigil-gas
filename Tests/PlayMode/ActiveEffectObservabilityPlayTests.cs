// PlayMode 测试：ASC 激活效果(ActiveGameplayEffect)可观测性——供 buff/debuff 图标条订阅。
//  A) 施加持续/永久效果触发 OnActiveEffectAdded + 进入只读枚举；
//  B) 瞬时效果不产生激活实例：不触发 Added、不进枚举；
//  C) 显式移除触发 OnActiveEffectRemoved + 退出枚举；
//  D) 限时效果到期自动移除时同样触发 OnActiveEffectRemoved；
//  E) 按句柄取激活实例可读到剩余时长（驱动倒计时图标）。
// 放 PlayMode：依赖 ASC.Update 每帧推进时长（到期分支）。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class ActiveEffectObservabilityPlayTests
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

        // MaxHealth +amount，时长可控（Infinite / HasDuration）
        private GameplayEffect NewMaxHealthBuff(float amount, EGameplayEffectDurationType durationType, float duration = 0f)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = durationType;
            ge.Duration = duration;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().MaxHealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(amount)
            });
            return ge;
        }

        // 瞬时改 Health 基础值（不产生激活实例）
        private GameplayEffect NewInstantHealthDelta(float amount)
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>(); _assets.Add(ge);
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = new AS_Health().HealthAttribute,
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(amount)
            });
            return ge;
        }

        // ============ A) Added 事件 + 只读枚举 ============
        [UnityTest]
        public IEnumerator A_ApplyDurationEffect_FiresAdded_AndAppearsInEnumeration()
        {
            var asc = NewASC();
            ActiveGameplayEffect added = null;
            int addedCount = 0;
            asc.OnActiveEffectAdded += e => { added = e; addedCount++; };

            var handle = asc.ApplyGameplayEffectToSelf(NewMaxHealthBuff(50f, EGameplayEffectDurationType.Infinite));

            Assert.IsTrue(handle.IsValid, "持续效果应返回有效句柄");
            Assert.AreEqual(1, addedCount, "应恰好触发一次 OnActiveEffectAdded");
            Assert.IsNotNull(added, "Added 回调应携带激活效果实例");
            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count, "只读枚举应含 1 个激活效果");
            Assert.AreSame(added, asc.GetActiveGameplayEffects()[0], "枚举里的实例应与 Added 回调一致");
            yield return null;
        }

        // ============ B) 瞬时效果不可观测为激活效果 ============
        [UnityTest]
        public IEnumerator B_InstantEffect_DoesNotFireAdded_NorEnumerate()
        {
            var asc = NewASC();
            int addedCount = 0;
            asc.OnActiveEffectAdded += _ => addedCount++;

            var handle = asc.ApplyGameplayEffectToSelf(NewInstantHealthDelta(-10f));

            Assert.IsFalse(handle.IsValid, "瞬时效果不产生激活句柄");
            Assert.AreEqual(0, addedCount, "瞬时效果不应触发 OnActiveEffectAdded");
            Assert.AreEqual(0, asc.GetActiveGameplayEffects().Count, "瞬时效果不应进入激活枚举");
            yield return null;
        }

        // ============ C) 显式移除触发 Removed ============
        [UnityTest]
        public IEnumerator C_ExplicitRemove_FiresRemoved_AndLeavesEnumeration()
        {
            var asc = NewASC();
            ActiveGameplayEffect removed = null;
            int removedCount = 0;
            asc.OnActiveEffectRemoved += e => { removed = e; removedCount++; };

            var handle = asc.ApplyGameplayEffectToSelf(NewMaxHealthBuff(50f, EGameplayEffectDurationType.Infinite));
            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count);

            bool ok = asc.RemoveActiveGameplayEffect(handle);

            Assert.IsTrue(ok, "移除应成功");
            Assert.AreEqual(1, removedCount, "应恰好触发一次 OnActiveEffectRemoved");
            Assert.IsNotNull(removed, "Removed 回调应携带被移除的激活效果");
            Assert.AreEqual(0, asc.GetActiveGameplayEffects().Count, "移除后枚举应清空");
            Assert.IsNull(asc.GetActiveGameplayEffect(handle), "按句柄已查不到");
            yield return null;
        }

        // ============ D) 到期自动移除也触发 Removed ============
        [UnityTest]
        public IEnumerator D_ExpiryAutoRemove_FiresRemoved()
        {
            var asc = NewASC();
            int removedCount = 0;
            asc.OnActiveEffectRemoved += _ => removedCount++;

            asc.ApplyGameplayEffectToSelf(NewMaxHealthBuff(50f, EGameplayEffectDurationType.HasDuration, 0.1f));
            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count, "施加后应有 1 个激活效果");

            yield return new WaitForSeconds(0.25f); // 让 ASC.Update 推进过期

            Assert.AreEqual(1, removedCount, "到期应触发一次 OnActiveEffectRemoved");
            Assert.AreEqual(0, asc.GetActiveGameplayEffects().Count, "到期后枚举应清空");
        }

        // ============ E) 按句柄读剩余时长（倒计时图标） ============
        [UnityTest]
        public IEnumerator E_GetActiveEffectByHandle_ExposesTimeRemaining()
        {
            var asc = NewASC();
            var handle = asc.ApplyGameplayEffectToSelf(NewMaxHealthBuff(50f, EGameplayEffectDurationType.HasDuration, 5f));

            var active = asc.GetActiveGameplayEffect(handle);
            Assert.IsNotNull(active, "按句柄应取到激活效果实例");
            Assert.Greater(active.TimeRemaining, 0f, "限时效果应有正剩余时长");
            Assert.LessOrEqual(active.TimeRemaining, 5f, "剩余时长不应超过总时长");
            yield return null;
        }
    }
}
