// GASDemo：运行时构建一个可玩 demo 场景，演示 输入→移动→近战命中→扣血→Cue 的完整闭环。
// 把本组件挂到空场景的一个 GameObject 上，按 Play 即可游玩（WASD 移动 / Shift 冲刺 / 鼠标看 / 空格或左键攻击）。
using System.Collections;
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace GASDemo
{
    public class GASDemo : MonoBehaviour
    {
        [Header("配置")]
        public float Damage = 20f;
        public Vector3 EnemySpawn = new Vector3(0, 1, 4);

        // 供测试/外部读取
        public AbilitySystemComponent PlayerASC { get; private set; }
        public AbilitySystemComponent EnemyASC { get; private set; }
        public MeleeAttackTrace Melee { get; private set; }
        public DemoPlayerController Controller { get; private set; }

        private static readonly GameplayTag MeleeTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        private static readonly GameplayTag DataDamage = GameplayTag.RequestTag("Data.Damage");
        private static readonly GameplayTag HitCue = GameplayTag.RequestTag("GameplayCue.Hit");

        private readonly List<Object> _runtimeAssets = new List<Object>();

        private void Awake()
        {
            BuildGround();
            var cam = EnsureCamera();
            var player = BuildPlayer();
            BuildEnemy(EnemySpawn);
            WireCamera(cam, player);
            RegisterHitCue();
        }

        private void OnDestroy()
        {
            GameplayCueManager.Instance.OnGameplayCue -= OnCue;
            foreach (var a in _runtimeAssets) if (a != null) Destroy(a);
        }

        // ---------- 场景元素 ----------
        private void BuildGround()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(transform, true);
            g.transform.localScale = new Vector3(5, 1, 5);
            SetColor(g, new Color(0.24f, 0.26f, 0.30f));
            g.AddComponent<SurfaceType>().Surface = GameplayTag.RequestTag("SurfaceType.Stone");
        }

        private Camera EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0, 3, -6);
            return cam;
        }

        private GameObject BuildPlayer()
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            p.name = "Player";
            p.transform.position = new Vector3(0, 1, 0);
            p.transform.SetParent(transform, true);
            SetColor(p, new Color(0.22f, 0.5f, 0.9f));
            Destroy(p.GetComponent<Collider>()); // 用 CharacterController 作碰撞

            var cc = p.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;

            var asc = p.AddComponent<AbilitySystemComponent>();
            asc.AddAttributeSet(new AS_Health());
            asc.AddAttributeSet(new AS_Stamina());

            var move = p.AddComponent<CharacterMovementSystemComponent>(); // Awake 接 ASC + CharacterController
            p.AddComponent<CombatTeamAgent>().SetTeamId(0);
            p.AddComponent<CombatSystemComponent>();
            var melee = p.AddComponent<MeleeAttackTrace>();

            // 武器判定点（角色前方）
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(p.transform);
            socket.localPosition = new Vector3(0, 0, 1.2f);

            // 攻击定义（运行时）
            var damageGE = MakeDamageGE();
            var attack = Track(ScriptableObject.CreateInstance<AttackDefinition>());
            attack.TargetEffect = damageGE;
            attack.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = DataDamage, Value = Damage });
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = attack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.0f }
            });

            // 移动定义（运行时）
            var def = Track(ScriptableObject.CreateInstance<MovementDefinition>());
            var set = new MovementSetSetting { MovementSet = GameplayTag.RequestTag("Movement.Set.Default") };
            set.States.Add(MakeState(MovementTags.MovementState_Walk, 2f));
            set.States.Add(MakeState(MovementTags.MovementState_Jog, 5f));
            set.States.Add(MakeState(MovementTags.MovementState_Sprint, 8f));
            def.MovementSets.Add(set);
            move.PushAvailableMovementDefinition(def);
            move.SetMovementSet(set.MovementSet);
            move.SetDesiredMovement(MovementTags.MovementState_Jog);
            move.SetDesiredRotationMode(MovementTags.RotationMode_VelocityDirection); // 朝移动方向

            // 近战技能（带体力消耗）
            var ability = Track(ScriptableObject.CreateInstance<DemoMeleeAbility>());
            ability.AbilityTags.Add(MeleeTag);
            ability.CostEffect = MakeStaminaCostGE();
            asc.GiveAbility(ability);

            var ctrl = p.AddComponent<DemoPlayerController>();
            ctrl.ASC = asc; ctrl.Movement = move; ctrl.Melee = melee;

            PlayerASC = asc; Melee = melee; Controller = ctrl;
            return p;
        }

        private void BuildEnemy(Vector3 pos)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            e.name = "Enemy";
            e.transform.position = pos;
            e.transform.SetParent(transform, true);
            SetColor(e, new Color(0.85f, 0.3f, 0.28f)); // 保留 CapsuleCollider 供判定命中

            var asc = e.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);
            e.AddComponent<CombatSystemComponent>();
            e.AddComponent<CombatTeamAgent>().SetTeamId(1);

            var bar = new GameObject("EnemyHealthBar").AddComponent<DemoHealthBar>();
            bar.Init(asc);

            // 受击闪红
            var rend = e.GetComponent<Renderer>();
            var baseColor = new Color(0.85f, 0.3f, 0.28f);
            asc.OnAttributeChanged += (attr, oldV, newV) =>
            {
                if (attr == health.HealthAttribute && newV < oldV) StartCoroutine(FlashRed(rend, baseColor));
            };

            EnemyASC = asc;
        }

        private void WireCamera(Camera cam, GameObject player)
        {
            var camMode = new ThirdPersonCameraBehavior { ArmLength = 5f, PivotOffset = new Vector3(0, 1.6f, 0) };
            var camSys = cam.gameObject.AddComponent<CameraSystemComponent>();
            camSys.Configure(cam, player.transform, camMode);
            if (Controller != null) Controller.ThirdPersonCamera = camMode;
        }

        // ---------- GameplayCue：命中火花 ----------
        private void RegisterHitCue() => GameplayCueManager.Instance.OnGameplayCue += OnCue;

        private void OnCue(GameObject target, GameplayTag cueTag, EGameplayCueEvent ev, GameplayCueParameters p)
        {
            if (ev == EGameplayCueEvent.Executed && cueTag.MatchesTag(HitCue))
                StartCoroutine(HitSpark(p != null ? p.Location : Vector3.zero));
        }

        private IEnumerator HitSpark(Vector3 loc)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(s.GetComponent<Collider>());
            s.transform.position = loc;
            SetColor(s, new Color(1f, 0.85f, 0.2f));
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                s.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.6f, t / 0.2f);
                yield return null;
            }
            Destroy(s);
        }

        private static IEnumerator FlashRed(Renderer rend, Color baseColor)
        {
            SetColor(rend, Color.red);
            yield return new WaitForSeconds(0.1f);
            if (rend != null) SetColor(rend, baseColor);
        }

        // ---------- 运行时资产构造 ----------
        private GameplayEffect MakeDamageGE()
        {
            var ge = Track(ScriptableObject.CreateInstance<GameplayEffect>());
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("IncomingDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.SetByCaller(DataDamage)
            });
            ge.GameplayCues.Add(HitCue);
            return ge;
        }

        private GameplayEffect MakeStaminaCostGE()
        {
            var ge = Track(ScriptableObject.CreateInstance<GameplayEffect>());
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Stamina>("Stamina"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(-8f)
            });
            return ge;
        }

        private static MovementStateSetting MakeState(GameplayTag state, float speed) => new MovementStateSetting
        {
            State = state, Speed = speed, Acceleration = 25f, BrakingDeceleration = 30f, RotationInterpolationSpeed = 12f
        };

        private T Track<T>(T asset) where T : Object { _runtimeAssets.Add(asset); return asset; }

        // URP/标准 通用着色
        private static void SetColor(GameObject go, Color c) => SetColor(go.GetComponent<Renderer>(), c);
        private static void SetColor(Renderer rend, Color c)
        {
            if (rend == null) return;
            var m = rend.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.color = c;
        }
    }
}
