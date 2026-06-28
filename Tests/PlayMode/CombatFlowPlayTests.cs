// PlayMode 测试：CombatFlow 受击反应管线（CombatFlowComponent + AttackResultProcessor）。
//  A) 管线对每个 AttackResult 跑处理器，并传入正确上下文（受击方/攻击方）。
//  B) Death 处理器：Health<=0 → 挂死亡标签 + 死亡事件。
//  C) GameplayEvent 处理器：按目标标签查询过滤（命中触发 / 不命中跳过）。
//  D) GameplayEvent 处理器：SendToAttacker → 事件发给攻击方。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 记录调用与上下文的测试处理器
    public class RecorderProcessor : AttackResultProcessor
    {
        public int Calls;
        public AbilitySystemComponent Owner;
        public AbilitySystemComponent Attacker;
        public override void Process(AttackResult result, in AttackFlowContext ctx)
        {
            Calls++; Owner = ctx.OwnerASC; Attacker = ctx.AttackerASC;
        }
    }

    public class CombatFlowPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        private GameObject NewGo(string name) { var go = new GameObject(name); _spawned.Add(go); return go; }

        private (AbilitySystemComponent asc, CombatSystemComponent combat, CombatFlowComponent flow) NewFighter(bool withHealth)
        {
            var go = NewGo("Fighter");
            var asc = go.AddComponent<AbilitySystemComponent>();
            if (withHealth) asc.AddAttributeSet(new AS_Health());
            var combat = go.AddComponent<CombatSystemComponent>();
            var flow = go.AddComponent<CombatFlowComponent>();
            return (asc, combat, flow);
        }

        private AbilitySystemComponent NewAttacker()
        {
            var go = NewGo("Attacker");
            return go.AddComponent<AbilitySystemComponent>();
        }

        private AttackResult MakeResult(AbilitySystemComponent attacker, params GameplayTag[] targetTags)
        {
            var r = new AttackResult
            {
                ImpactResult = Tag("Combat.Impact.Hit"),
                EffectContext = new GameplayEffectContext(attacker),
                HitLocation = Vector3.zero
            };
            foreach (var t in targetTags) r.AggregatedTargetTags.AddTag(t);
            return r;
        }

        // ============ A) 管线 + 上下文 ============
        [UnityTest]
        public IEnumerator A_Pipeline_RunsWithContext()
        {
            var (ownerAsc, combat, flow) = NewFighter(false);
            var attacker = NewAttacker();
            var rec = new RecorderProcessor();
            flow.Processors.Add(rec);
            yield return null;

            combat.RegisterAttackResult(MakeResult(attacker));

            Assert.AreEqual(1, rec.Calls, "应对结果跑一次处理器");
            Assert.AreSame(ownerAsc, rec.Owner, "上下文受击方应为本体");
            Assert.AreSame(attacker, rec.Attacker, "上下文攻击方应来自 EffectContext.SourceASC");
        }

        // ============ B) 死亡处理 ============
        [UnityTest]
        public IEnumerator B_Death_TagAndEvent()
        {
            var (ownerAsc, combat, flow) = NewFighter(true);
            var attacker = NewAttacker();
            flow.Processors.Add(new AttackResultProcessor_Death
            {
                DeadTag = Tag("State.Dead"),
                DeathEventTag = Tag("Event.Death")
            });
            yield return null;

            int deathEvents = 0;
            ownerAsc.OnGameplayEvent += (t, d) => { if (t.MatchesTagExact(Tag("Event.Death"))) deathEvents++; };

            // 先把血打到 0
            var hp = ownerAsc.GetAttributeSet<AS_Health>();
            ownerAsc.ApplyModToAttributeBase(hp.HealthAttribute, EAttributeModifierOp.Override, 0f);

            combat.RegisterAttackResult(MakeResult(attacker));

            Assert.IsTrue(ownerAsc.HasMatchingGameplayTag(Tag("State.Dead")), "死亡应挂 State.Dead");
            Assert.AreEqual(1, deathEvents, "应触发死亡事件");
        }

        // ============ C) 事件处理器 + 目标标签过滤 ============
        [UnityTest]
        public IEnumerator C_GameplayEvent_TargetTagFilter()
        {
            var (ownerAsc, combat, flow) = NewFighter(false);
            var attacker = NewAttacker();
            flow.Processors.Add(new AttackResultProcessor_GameplayEvent
            {
                TargetTagQuery = GameplayTagQuery.MakeQuery_MatchAllTags(Tag("State.Vulnerable")),
                EventTriggers = new List<GameplayTag> { Tag("Event.HitReact") }
            });
            yield return null;

            int reacts = 0;
            ownerAsc.OnGameplayEvent += (t, d) => { if (t.MatchesTagExact(Tag("Event.HitReact"))) reacts++; };

            // 不含 Vulnerable → 被过滤
            combat.RegisterAttackResult(MakeResult(attacker));
            Assert.AreEqual(0, reacts, "目标无 Vulnerable 标签应被过滤");

            // 含 Vulnerable → 触发
            combat.RegisterAttackResult(MakeResult(attacker, Tag("State.Vulnerable")));
            Assert.AreEqual(1, reacts, "目标含 Vulnerable 应触发事件");
        }

        // ============ D) 发给攻击方 ============
        [UnityTest]
        public IEnumerator D_GameplayEvent_SendToAttacker()
        {
            var (ownerAsc, combat, flow) = NewFighter(false);
            var attacker = NewAttacker();
            flow.Processors.Add(new AttackResultProcessor_GameplayEvent
            {
                SendToAttacker = true,
                EventTriggers = new List<GameplayTag> { Tag("Event.Riposte") }
            });
            yield return null;

            int attackerEvents = 0, ownerEvents = 0;
            attacker.OnGameplayEvent += (t, d) => { if (t.MatchesTagExact(Tag("Event.Riposte"))) attackerEvents++; };
            ownerAsc.OnGameplayEvent += (t, d) => { if (t.MatchesTagExact(Tag("Event.Riposte"))) ownerEvents++; };

            combat.RegisterAttackResult(MakeResult(attacker));

            Assert.AreEqual(1, attackerEvents, "事件应发给攻击方");
            Assert.AreEqual(0, ownerEvents, "事件不应发给受击方");
        }
    }
}
