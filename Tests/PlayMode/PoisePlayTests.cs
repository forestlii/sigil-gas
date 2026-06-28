// PlayMode 测试：Poise 削韧（AS_Poise + PoiseComponent）。
//  A) 削韧归零 → 破防：挂硬直标签、IsStaggered、OnPoiseBroken 触发。
//  B) 硬直结束（staggerDuration=0）→ 复位：解标签、Poise 回满、OnPoiseRecovered。
//  C) 未硬直时按 PoiseRecover 速率恢复（方向性，headless dt 极小只断言递增）。
//  D) AS_Poise 自身：Poise 夹到 [0,MaxPoise]。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    public class PoisePlayTests
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

        // 削韧伤害 GE：IncomingPoiseDamage += dmg
        private GameplayEffect MakePoiseDamageGE(float dmg)
        {
            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Poise>("IncomingPoiseDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(dmg)
            });
            return ge;
        }

        private (AbilitySystemComponent asc, AS_Poise poise, PoiseComponent comp) NewPoiseActor()
        {
            var go = NewGo("Fighter");
            var asc = go.AddComponent<AbilitySystemComponent>();
            var poise = new AS_Poise();          // Poise=3, MaxPoise=3, PoiseRecover=1
            asc.AddAttributeSet(poise);
            var comp = go.AddComponent<PoiseComponent>();
            return (asc, poise, comp);
        }

        // ============ A) 破防 ============
        [UnityTest]
        public IEnumerator A_PoiseBreak_OnZero()
        {
            var (asc, poise, comp) = NewPoiseActor();
            yield return null; // 等 OnEnable 订阅

            int broken = 0; comp.OnPoiseBroken += () => broken++;

            asc.ApplyGameplayEffectToSelf(MakePoiseDamageGE(3f)); // 3 → 0

            Assert.AreEqual(0f, poise.Poise.CurrentValue, 0.01f, "削韧应归零");
            Assert.IsTrue(comp.IsStaggered, "归零应破防");
            Assert.IsTrue(asc.HasMatchingGameplayTag(comp.StaggeredTag), "破防应挂硬直标签");
            Assert.AreEqual(1, broken, "应触发一次 OnPoiseBroken");
        }

        // ============ B) 硬直结束复位 ============
        [UnityTest]
        public IEnumerator B_Recover_AfterStagger()
        {
            var (asc, poise, comp) = NewPoiseActor();
            comp.StaggerDuration = 0f; // 下一帧 Update 即恢复
            yield return null;

            int recovered = 0; comp.OnPoiseRecovered += () => recovered++;

            asc.ApplyGameplayEffectToSelf(MakePoiseDamageGE(3f)); // 破防
            Assert.IsTrue(comp.IsStaggered);

            yield return null; // Update 检测 staggerTimer<=0 → Recover
            yield return null;

            Assert.IsFalse(comp.IsStaggered, "硬直应结束");
            Assert.IsFalse(asc.HasMatchingGameplayTag(comp.StaggeredTag), "硬直标签应解除");
            Assert.AreEqual(poise.MaxPoise.CurrentValue, poise.Poise.CurrentValue, 0.01f, "Poise 应回满");
            Assert.AreEqual(1, recovered, "应触发一次 OnPoiseRecovered");
        }

        // ============ C) 恢复速率（方向性）============
        [UnityTest]
        public IEnumerator C_PoiseRegen_WhenNotStaggered()
        {
            var (asc, poise, comp) = NewPoiseActor();
            comp.RecoverDelay = 0f; // 立即恢复
            yield return null;

            asc.ApplyGameplayEffectToSelf(MakePoiseDamageGE(2f)); // 3 → 1（未破防）
            Assert.IsFalse(comp.IsStaggered, "未归零不应破防");
            float afterHit = poise.Poise.CurrentValue; // 1

            for (int i = 0; i < 30; i++) yield return null; // 累积若干帧恢复

            Assert.Greater(poise.Poise.CurrentValue, afterHit, "未硬直时 Poise 应随时间恢复");
            Assert.LessOrEqual(poise.Poise.CurrentValue, poise.MaxPoise.CurrentValue + 0.001f, "不应超过上限");
        }

        // ============ D) AS_Poise clamp ============
        [UnityTest]
        public IEnumerator D_Poise_ClampedToMax()
        {
            var (asc, poise, comp) = NewPoiseActor();
            comp.RecoverDelay = 999f; // 关掉自动恢复干扰
            yield return null;

            // 过量削韧不应让 Poise 变负
            asc.ApplyGameplayEffectToSelf(MakePoiseDamageGE(10f));
            Assert.AreEqual(0f, poise.Poise.CurrentValue, 0.01f, "Poise 不应为负");
        }
    }
}
