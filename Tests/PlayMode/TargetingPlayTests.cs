// PlayMode 测试：TargetActor / TargetData / TargetSource / WaitTargetData（阶段1 补强）。
//  A) LineTrace 命中正前方目标，句柄取到目标 ASC。
//  B) 偏移目标：SphereTrace 半径能兜住、LineTrace 漏检（仅终点）。
//  C) 过滤器剔除指定 actor；自身被忽略。
//  D) WaitTargetData(Instant) → 拿到目标数据 → 施加伤害 → 扣血闭环。
//  E) TargetSource_Self / UseEventData。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 技能：激活时用 LineTrace 采集，对命中的每个 ASC 施加固定伤害。
    public class TraceDamageAbility : GameplayAbility
    {
        public GameplayEffect Damage;
        public int HitCount;

        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            var actor = new TargetActor_LineTrace
            {
                StartTransform = ASC.transform,
                MaxRange = 10f,
                MaxHitResultsPerTrace = 1
            };
            var task = AbilityTask_WaitTargetData.WaitTargetData(this, actor);
            task.OnValidData += data =>
            {
                var ascs = data.GetTargetASCs();
                HitCount = ascs.Count;
                foreach (var t in ascs) t.ApplyGameplayEffectToSelf(Damage);
            };
            task.Activate();
            EndAbility();
        }
    }

    public class TargetingPlayTests
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

        // 默认朝向 +Z（Unity forward），目标放正前方
        private GameObject NewTargetAt(Vector3 pos, float radius, out AbilitySystemComponent asc)
        {
            var go = NewGo("Target");
            go.transform.position = pos;
            asc = go.AddComponent<AbilitySystemComponent>();
            var col = go.AddComponent<SphereCollider>(); col.radius = radius;
            return go;
        }

        private GameplayEffect MakeDamageGE(float dmg)
        {
            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("IncomingDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(dmg)
            });
            return ge;
        }

        // ============ A) LineTrace 命中正前方 ============
        [UnityTest]
        public IEnumerator A_LineTrace_HitsForwardTarget()
        {
            var source = NewGo("Source"); // 朝 +Z
            NewTargetAt(new Vector3(0, 0, 5), 1f, out var targetAsc);
            yield return new WaitForFixedUpdate();

            TargetDataHandle captured = null;
            var actor = new TargetActor_LineTrace { SourceActor = source, StartTransform = source.transform, MaxRange = 10f };
            actor.OnTargetDataReady += h => captured = h;
            actor.StartTargeting(null); // Instant

            Assert.IsNotNull(captured, "应回调目标数据");
            var hit = captured.GetHitResult(0);
            Assert.IsTrue(hit.IsValidBlockingHit, "应命中阻挡物");
            CollectionAssert.Contains(captured.GetTargetASCs(), targetAsc, "句柄应含目标 ASC");
        }

        // ============ B) 偏移目标：SphereTrace 兜住、LineTrace 漏 ============
        [UnityTest]
        public IEnumerator B_SphereCatches_LineMisses_OffsetTarget()
        {
            var source = NewGo("Source");
            NewTargetAt(new Vector3(1.2f, 0, 5), 0.5f, out var targetAsc); // X 偏 1.2
            yield return new WaitForFixedUpdate();

            // 直线正前方 → 漏（仅终点，无 actor）
            TargetDataHandle lineData = null;
            var line = new TargetActor_LineTrace { SourceActor = source, StartTransform = source.transform, MaxRange = 10f };
            line.OnTargetDataReady += h => lineData = h;
            line.StartTargeting(null);
            Assert.IsFalse(lineData.GetHitResult(0).IsValidBlockingHit, "直线应漏掉偏移目标（仅终点）");

            // 球扫半径 1.5 → 兜住
            TargetDataHandle sphereData = null;
            var sphere = new TargetActor_SphereTrace { SourceActor = source, StartTransform = source.transform, MaxRange = 10f, Radius = 1.5f };
            sphere.OnTargetDataReady += h => sphereData = h;
            sphere.StartTargeting(null);
            CollectionAssert.Contains(sphereData.GetTargetASCs(), targetAsc, "球扫应兜住偏移目标");
        }

        // ============ C) 过滤器剔除 + 忽略自身 ============
        [UnityTest]
        public IEnumerator C_Filter_And_IgnoreSelf()
        {
            var source = NewGo("Source");
            var selfCol = source.AddComponent<SphereCollider>(); selfCol.radius = 0.5f; // 自身也有碰撞体
            NewTargetAt(new Vector3(0, 0, 3), 1f, out var friendAsc);
            friendAsc.AddLooseGameplayTag(Tag("Team.Friendly"));
            NewTargetAt(new Vector3(0, 0, 6), 1f, out var enemyAsc);
            yield return new WaitForFixedUpdate();

            TargetDataHandle data = null;
            var actor = new TargetActor_LineTrace
            {
                SourceActor = source,
                StartTransform = source.transform,
                MaxRange = 10f,
                MaxHitResultsPerTrace = 5,
                Filter = go => { var a = go.GetComponent<AbilitySystemComponent>(); return a == null || !a.HasMatchingGameplayTag(Tag("Team.Friendly")); }
            };
            actor.OnTargetDataReady += h => data = h;
            actor.StartTargeting(null);

            var ascs = data.GetTargetASCs();
            CollectionAssert.DoesNotContain(ascs, friendAsc, "友军应被过滤掉");
            CollectionAssert.Contains(ascs, enemyAsc, "敌人应保留");
        }

        // ============ D) WaitTargetData → 施伤闭环 ============
        [UnityTest]
        public IEnumerator D_WaitTargetData_AppliesDamage()
        {
            var caster = NewGo("Caster");
            var casterAsc = caster.AddComponent<AbilitySystemComponent>();
            NewTargetAt(new Vector3(0, 0, 4), 1f, out var targetAsc);
            var health = new AS_Health(); targetAsc.AddAttributeSet(health);
            yield return new WaitForFixedUpdate();

            var ability = NewAsset<TraceDamageAbility>();
            ability.Damage = MakeDamageGE(20f);
            var handle = casterAsc.GiveAbility(ability);

            float before = health.Health.CurrentValue; // 100
            casterAsc.TryActivateAbility(handle);

            Assert.AreEqual(before - 20f, health.Health.CurrentValue, 0.1f, "WaitTargetData 命中后应扣血 20");
        }

        // ============ E) TargetSource ============
        [UnityTest]
        public IEnumerator E_TargetSource_Self_And_UseEventData()
        {
            var owner = NewGo("Owner");
            var ownerAsc = owner.AddComponent<AbilitySystemComponent>();
            var victim = NewGo("Victim");
            yield return null;

            var useOwner = NewAsset<TargetSource_Self>();
            var hits = new List<TargetHitResult>(); var actors = new List<GameObject>();
            useOwner.GetTargets(ownerAsc, new GameplayEventData(), hits, actors);
            CollectionAssert.Contains(actors, owner, "UseOwner 应返回施法者自身");

            var useEvent = NewAsset<TargetSource_EventData>();
            hits.Clear(); actors.Clear();
            var evt = new GameplayEventData { Target = victim };
            useEvent.GetTargets(ownerAsc, evt, hits, actors);
            CollectionAssert.Contains(actors, victim, "UseEventData 应返回事件目标");
        }
    }
}
