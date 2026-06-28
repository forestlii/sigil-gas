// PlayMode 测试：验证阶段 2/3 的运行时闭环。
//  A) 状态驱动按键多态：冲刺时同键触发滑铲、否则下蹲。
//  B) 近战判定：socket 球扫命中目标 → 施加伤害 → 扣血。
//  C) 完整闭环：输入 → 近战技能 → 开判定 → 命中目标 → 扣血。
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;

namespace Likeon.GAS.PlayTests
{
    // 激活后记录自己 Id 的测试技能（验证"哪个技能被激活"）
    public class FlagAbility : GameplayAbility
    {
        public static string LastActivatedId;
        public string Id;
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            LastActivatedId = Id;
            EndAbility();
        }
    }

    // 激活后开启近战判定窗口的测试技能
    public class MeleeStartAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            var trace = ASC.GetComponent<MeleeAttackTrace>();
            if (trace != null) trace.BeginAttackTrace(0);
            // 保持激活；测试结束时收尾
        }
    }

    public class CombatLoopPlayTests
    {
        private static GameplayTag Tag(string s) => GameplayTag.RequestTag(s);

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            foreach (var a in _assets) if (a != null) Object.Destroy(a);
            _spawned.Clear();
            _assets.Clear();
        }

        private GameObject NewGo(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            _assets.Add(a);
            return a;
        }

        // 造一个瞬时伤害 GE：对 AS_Health.IncomingDamage += SetByCaller(Data.Damage)
        private GameplayEffect MakeDamageGE()
        {
            var ge = NewAsset<GameplayEffect>();
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("IncomingDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.SetByCaller(Tag("Data.Damage"))
            });
            return ge;
        }

        // ============ A) 状态驱动按键多态 ============
        [UnityTest]
        public IEnumerator A_StateDrivenPolymorphism_SprintSlide_ElseCrouch()
        {
            var player = NewGo("Player");
            var asc = player.AddComponent<AbilitySystemComponent>();
            var ic = player.AddComponent<InputSystemComponent>();

            var slide = NewAsset<FlagAbility>(); slide.Id = "slide"; slide.AbilityTags.Add(Tag("Ability.Slide"));
            var crouch = NewAsset<FlagAbility>(); crouch.Id = "crouch"; crouch.AbilityTags.Add(Tag("Ability.Crouch"));
            asc.GiveAbility(slide);
            asc.GiveAbility(crouch);

            var inputTag = Tag("InputTag.Crouch");

            var setup = NewAsset<InputControlSetup>();
            setup.ExecutionType = EInputProcessorExecutionType.FirstOnly;

            // 滑铲处理器（排前）：需角色含 Sprint 才通过
            var slideP = new InputProcessor_ActivateAbilityByTag
            {
                AbilityTag = Tag("Ability.Slide"),
                StateQuery = GameplayTagQuery.MakeQuery_MatchAllTags(Tag("Movement.State.Sprint"))
            };
            slideP.InputTags.AddTag(inputTag);
            slideP.TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started };

            // 下蹲处理器（排后）：无条件
            var crouchP = new InputProcessor_ActivateAbilityByTag { AbilityTag = Tag("Ability.Crouch") };
            crouchP.InputTags.AddTag(inputTag);
            crouchP.TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started };

            setup.AddProcessor(slideP);
            setup.AddProcessor(crouchP);
            ic.PushInputSetup(setup);

            yield return null; // 等 Awake 把 abilitySystem 接上

            // 冲刺中按键 → 滑铲
            asc.AddLooseGameplayTag(Tag("Movement.State.Sprint"));
            FlagAbility.LastActivatedId = null;
            ic.ReceiveInput(inputTag, InputTriggerEvent.Started, InputActionData.Empty);
            Assert.AreEqual("slide", FlagAbility.LastActivatedId, "冲刺中同键应触发滑铲");

            // 非冲刺按键 → 下蹲
            asc.RemoveLooseGameplayTag(Tag("Movement.State.Sprint"));
            FlagAbility.LastActivatedId = null;
            ic.ReceiveInput(inputTag, InputTriggerEvent.Started, InputActionData.Empty);
            Assert.AreEqual("crouch", FlagAbility.LastActivatedId, "非冲刺同键应触发下蹲");
        }

        // ============ B) 近战判定直接命中 ============
        [UnityTest]
        public IEnumerator B_MeleeTrace_DealsDamage()
        {
            // 攻击者
            var attacker = NewGo("Attacker");
            var attackerAsc = attacker.AddComponent<AbilitySystemComponent>();
            attacker.AddComponent<CombatSystemComponent>();
            var teamA = attacker.AddComponent<CombatTeamAgent>(); teamA.SetTeamId(0);
            var trace = attacker.AddComponent<MeleeAttackTrace>();

            // socket（武器判定点），放到目标处
            var socket = NewGo("Socket").transform;
            socket.SetParent(attacker.transform);
            socket.position = new Vector3(5, 0, 0);

            // 目标
            var target = NewGo("Target");
            target.transform.position = new Vector3(5, 0, 0);
            var targetAsc = target.AddComponent<AbilitySystemComponent>();
            var targetHealth = new AS_Health();
            targetAsc.AddAttributeSet(targetHealth);
            target.AddComponent<CombatSystemComponent>();
            var teamB = target.AddComponent<CombatTeamAgent>(); teamB.SetTeamId(1);
            var col = target.AddComponent<SphereCollider>(); col.radius = 0.5f;

            // 攻击定义
            var atk = NewAsset<AttackDefinition>();
            atk.TargetEffect = MakeDamageGE();
            atk.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = Tag("Data.Damage"), Value = 30f });

            trace.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = atk,
                Trace = new CollisionTraceDefinition
                {
                    TraceTag = Tag("Combat.Trace.Weapon"),
                    SocketTransforms = new List<Transform> { socket },
                    TraceRadius = 1f
                }
            });

            yield return null; // Awake 接上 ASC/Combat

            float before = targetHealth.Health.CurrentValue; // 100
            trace.BeginAttackTrace(0);
            yield return new WaitForFixedUpdate(); // 物理同步
            yield return null;                     // 让 LateUpdate 跑判定
            trace.EndAttackTrace();

            Assert.AreEqual(before - 30f, targetHealth.Health.CurrentValue, 0.1f, "近战命中应扣血 30");
        }

        // ============ C) 完整闭环：输入 → 近战技能 → 命中 → 扣血 ============
        [UnityTest]
        public IEnumerator C_FullLoop_InputToMeleeToDamage()
        {
            var attacker = NewGo("Attacker");
            var attackerAsc = attacker.AddComponent<AbilitySystemComponent>();
            attacker.AddComponent<CombatSystemComponent>();
            (attacker.AddComponent<CombatTeamAgent>()).SetTeamId(0);
            var trace = attacker.AddComponent<MeleeAttackTrace>();
            var ic = attacker.AddComponent<InputSystemComponent>();

            var socket = NewGo("Socket").transform;
            socket.SetParent(attacker.transform);
            socket.position = new Vector3(2, 0, 0);

            var target = NewGo("Target");
            target.transform.position = new Vector3(2, 0, 0);
            var targetAsc = target.AddComponent<AbilitySystemComponent>();
            var targetHealth = new AS_Health();
            targetAsc.AddAttributeSet(targetHealth);
            target.AddComponent<CombatSystemComponent>();
            (target.AddComponent<CombatTeamAgent>()).SetTeamId(1);
            (target.AddComponent<SphereCollider>()).radius = 0.5f;

            var atk = NewAsset<AttackDefinition>();
            atk.TargetEffect = MakeDamageGE();
            atk.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = Tag("Data.Damage"), Value = 25f });
            trace.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = atk,
                Trace = new CollisionTraceDefinition
                {
                    SocketTransforms = new List<Transform> { socket },
                    TraceRadius = 1f
                }
            });

            // 近战技能：激活时开判定
            var melee = NewAsset<MeleeStartAbility>();
            melee.AbilityTags.Add(Tag("Ability.MeleeAttack"));
            attackerAsc.GiveAbility(melee);

            // 输入：攻击键 → 激活近战技能
            var inputTag = Tag("InputTag.Attack");
            var setup = NewAsset<InputControlSetup>();
            var atkP = new InputProcessor_ActivateAbilityByTag { AbilityTag = Tag("Ability.MeleeAttack") };
            atkP.InputTags.AddTag(inputTag);
            atkP.TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started };
            setup.AddProcessor(atkP);
            ic.PushInputSetup(setup);

            yield return null;

            float before = targetHealth.Health.CurrentValue;
            ic.ReceiveInput(inputTag, InputTriggerEvent.Started, InputActionData.Empty); // → 激活技能 → 开判定
            Assert.IsTrue(trace.IsTracing, "技能激活后应已开启判定窗口");

            yield return new WaitForFixedUpdate();
            yield return null;
            trace.EndAttackTrace();

            Assert.Less(targetHealth.Health.CurrentValue, before, "完整闭环：输入→技能→命中应扣血");
            Assert.AreEqual(before - 25f, targetHealth.Health.CurrentValue, 0.1f);
        }
    }
}
