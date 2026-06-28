// GASDemo：运行时构建一个可玩的"功能展示场"，演示框架已实现的多条战斗线——
//   近战命中→扣血→Cue   远程子弹   锁定切换   削韧破防   buff 叠层   第三人称相机 + 血条 + HUD。
// 把本组件挂到空场景的一个 GameObject 上，按 Play 即可游玩。
// 操作：WASD 移动 / Shift 冲刺 / 鼠标看 / 空格·左键近战 / 右键·F 远程 / Tab 锁定 / Q·E 切目标 / R 叠 buff。
// 注：本 demo 只是把核心包里"已经实现并测试过"的功能调用出来给人看，不含任何战斗逻辑本身。
using System.Collections;
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace GASDemo
{
    public class GASDemo : MonoBehaviour
    {
        [Header("配置")]
        public float MeleeDamage = 20f;
        public float MeleePoiseDamage = 1.5f;
        public float RangedDamage = 12f;
        public float RangedPoiseDamage = 1f;

        // 敌人摆位（前方扇形，便于演示锁定左右切换）
        public Vector3[] EnemySpawns =
        {
            new Vector3(-3.5f, 1, 5),
            new Vector3(0f, 1, 6.5f),
            new Vector3(3.5f, 1, 5),
        };

        // 供测试/外部读取
        public AbilitySystemComponent PlayerASC { get; private set; }
        public MeleeAttackTrace Melee { get; private set; }
        public DemoPlayerController Controller { get; private set; }
        public TargetingSystemComponent Targeting { get; private set; }
        public readonly List<AbilitySystemComponent> Enemies = new List<AbilitySystemComponent>();

        private static readonly GameplayTag MeleeTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        private static readonly GameplayTag RangedTag = GameplayTag.RequestTag("Ability.RangedAttack");
        private static readonly GameplayTag DataDamage = GameplayTag.RequestTag("Data.Damage");
        private static readonly GameplayTag DataPoiseDamage = GameplayTag.RequestTag("Data.PoiseDamage");
        private static readonly GameplayTag HitCue = GameplayTag.RequestTag("GameplayCue.Hit");

        private static readonly Color EnemyColor = new Color(0.85f, 0.3f, 0.28f);
        private static readonly Color StaggerColor = new Color(0.95f, 0.85f, 0.35f);

        private readonly List<Object> _runtimeAssets = new List<Object>();
        private GameplayEffect _damageGE; // 近战/远程共用的伤害效果（含削韧）

        private void Awake()
        {
            BuildGround();
            var cam = EnsureCamera();
            _damageGE = MakeDamageGE();
            var player = BuildPlayer(cam);
            foreach (var pos in EnemySpawns) BuildEnemy(pos);
            WireCamera(cam, player);
            BuildHUD();
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

        private GameObject BuildPlayer(Camera cam)
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

            p.AddComponent<CombatTeamAgent>().SetTeamId(0);
            p.AddComponent<CombatSystemComponent>();
            var melee = p.AddComponent<MeleeAttackTrace>();

            // 武器判定点（角色前方）
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(p.transform);
            socket.localPosition = new Vector3(0, 0, 1.2f);

            // 近战攻击定义（含削韧伤害）
            var meleeAttack = Track(MakeAttack(MeleeDamage, MeleePoiseDamage));
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = meleeAttack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.0f }
            });

            // 近战技能（带体力消耗）
            var meleeAbility = Track(ScriptableObject.CreateInstance<DemoMeleeAbility>());
            meleeAbility.AbilityTags.Add(MeleeTag);
            meleeAbility.CostEffect = MakeStaminaCostGE(-8f);
            asc.GiveAbility(meleeAbility);

            // 锁定系统（视角来源=相机）
            var targeting = p.AddComponent<TargetingSystemComponent>();
            targeting.ViewSource = cam.transform;

            // 远程：枪口 + 子弹定义 + 射击组件 + 远程技能
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(p.transform);
            muzzle.localPosition = new Vector3(0, 0.2f, 0.6f);

            var bulletDef = Track(ScriptableObject.CreateInstance<BulletDefinition>());
            bulletDef.name = "DemoBullet";
            bulletDef.InitialSpeed = 22f; bulletDef.HitRadius = 0.3f; bulletDef.Duration = 3f; bulletDef.GravityScale = 0f;
            bulletDef.Attack = Track(MakeAttack(RangedDamage, RangedPoiseDamage));

            var shooter = p.AddComponent<DemoRanged>();
            shooter.ASC = asc; shooter.Muzzle = muzzle; shooter.Bullet = bulletDef; shooter.Targeting = targeting;

            var rangedAbility = Track(ScriptableObject.CreateInstance<DemoRangedAbility>());
            rangedAbility.AbilityTags.Add(RangedTag);
            rangedAbility.CostEffect = MakeStaminaCostGE(-5f);
            asc.GiveAbility(rangedAbility);

            var ctrl = p.AddComponent<DemoPlayerController>();
            ctrl.ASC = asc; ctrl.Controller = cc; ctrl.Melee = melee; ctrl.Targeting = targeting;
            ctrl.PowerBuff = Track(MakePowerBuff());

            PlayerASC = asc; Melee = melee; Controller = ctrl; Targeting = targeting;
            return p;
        }

        private void BuildEnemy(Vector3 pos)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            e.name = "Enemy";
            e.transform.position = pos;
            e.transform.SetParent(transform, true);
            SetColor(e, EnemyColor); // 保留 CapsuleCollider 供命中 + 锁定候选

            var asc = e.AddComponent<AbilitySystemComponent>();
            var health = new AS_Health();
            asc.AddAttributeSet(health);
            asc.AddAttributeSet(new AS_Poise());   // 削韧属性
            e.AddComponent<CombatSystemComponent>();
            e.AddComponent<CombatTeamAgent>().SetTeamId(1);

            // 削韧机制（破防→硬直→恢复）
            var poiseComp = e.AddComponent<PoiseComponent>();

            var bar = new GameObject("EnemyHealthBar").AddComponent<DemoHealthBar>();
            bar.Init(asc);

            // 受击闪红 + 破防变色
            var rend = e.GetComponent<Renderer>();
            asc.OnAttributeChanged += (attr, oldV, newV) =>
            {
                if (attr == health.HealthAttribute && newV < oldV && !poiseComp.IsStaggered)
                    StartCoroutine(FlashRed(rend));
            };
            poiseComp.OnPoiseBroken += () => SetColor(rend, StaggerColor);
            poiseComp.OnPoiseRecovered += () => SetColor(rend, EnemyColor);

            Enemies.Add(asc);
        }

        private void WireCamera(Camera cam, GameObject player)
        {
            var camMode = new ThirdPersonCameraBehavior { ArmLength = 5f, PivotOffset = new Vector3(0, 1.6f, 0) };
            var camSys = cam.gameObject.AddComponent<CameraSystemComponent>();
            camSys.Configure(cam, player.transform, camMode);
            if (Controller != null) Controller.ThirdPersonCamera = camMode;
        }

        private void BuildHUD()
        {
            var hud = new GameObject("DemoHUD").AddComponent<DemoHUD>();
            hud.transform.SetParent(transform, false);
            hud.PlayerASC = PlayerASC;
            hud.Targeting = Targeting;
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

        private IEnumerator FlashRed(Renderer rend)
        {
            SetColor(rend, Color.red);
            yield return new WaitForSeconds(0.1f);
            if (rend != null) SetColor(rend, EnemyColor);
        }

        // ---------- 运行时资产构造 ----------
        // 伤害效果：扣血(SetByCaller Data.Damage) + 削韧(SetByCaller Data.PoiseDamage) + 命中 cue
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
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Poise>("IncomingPoiseDamage"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.SetByCaller(DataPoiseDamage)
            });
            ge.GameplayCues.Add(HitCue);
            return ge;
        }

        private AttackDefinition MakeAttack(float damage, float poiseDamage)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            attack.TargetEffect = _damageGE;
            attack.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = DataDamage, Value = damage });
            attack.SetByCallerMagnitudes.Add(new SetByCallerMagnitude { Tag = DataPoiseDamage, Value = poiseDamage });
            return attack;
        }

        private GameplayEffect MakeStaminaCostGE(float amount)
        {
            var ge = Track(ScriptableObject.CreateInstance<GameplayEffect>());
            ge.DurationType = EGameplayEffectDurationType.Instant;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Stamina>("Stamina"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(amount)
            });
            return ge;
        }

        // 叠层 buff：每层 +10 MaxHealth，限时 5s、按目标合并、上限 5 层、再施加刷新时长
        private GameplayEffect MakePowerBuff()
        {
            var ge = ScriptableObject.CreateInstance<GameplayEffect>();
            ge.name = "Power";
            ge.DurationType = EGameplayEffectDurationType.HasDuration;
            ge.Duration = 5f;
            ge.StackingType = EGameplayEffectStackingType.AggregateByTarget;
            ge.StackLimitCount = 5;
            ge.Modifiers.Add(new GameplayModifierInfo
            {
                Attribute = GameplayAttribute.From<AS_Health>("MaxHealth"),
                Operation = EAttributeModifierOp.Add,
                Magnitude = GameplayModifierMagnitude.ScalableFloat(10f)
            });
            return ge;
        }

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
