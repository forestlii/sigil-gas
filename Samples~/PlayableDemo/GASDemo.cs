// GASDemo：运行时构建一个可玩的"功能展示场"，演示框架已实现的多条战斗线——
//   近战命中→扣血→Cue   远程子弹   锁定切换   削韧破防   buff 叠层   第三人称相机 + 血条 + HUD，
//   以及 输入分发(键→tag→技能) / 载具切换 / 技能 block-cancel / 武器换技能。
// 把本组件挂到空场景的一个 GameObject 上，按 Play 即可游玩。
//
// 数据驱动：所有可配置数据（输入控制集 / 技能互斥规则 / 技能 / 攻击 / 子弹 / 效果）来自 Config(DemoConfig 资产)——
//   · 场景里把 DemoConfig.asset 拖到下面的 Config 字段 → 策划在 Inspector 改这些 .asset 即可（真实工作流）；
//   · Config 留空 → 用 DemoConfig.CreateDefault() 在内存里建同一套默认值（裸 AddComponent / headless 测试的回退）。
// 注：本 demo 只是把核心包里"已经实现并测试过"的功能调用出来给人看，不含任何战斗逻辑本身。
using System.Collections;
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace GASDemo
{
    public class GASDemo : MonoBehaviour
    {
        [Header("数据驱动配置（拖一个 DemoConfig.asset；留空=用代码默认）")]
        [Tooltip("策划在此资产里配输入分发/技能互斥/技能/攻击/效果。用菜单 Likeon ▸ GAS ▸ Generate Demo Config Assets 可一键生成一套并接好。")]
        public DemoConfig Config;

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
        public InputSystemComponent InputSystem { get; private set; }
        public DemoConfig ActiveConfig => _config;
        public readonly List<AbilitySystemComponent> Enemies = new List<AbilitySystemComponent>();

        private static readonly Color EnemyColor = new Color(0.85f, 0.3f, 0.28f);
        private static readonly Color StaggerColor = new Color(0.95f, 0.85f, 0.35f);

        private DemoConfig _config;
        private bool _ownsConfig; // 默认配置由本组件 new 出来 → 销毁时负责清理；拖进来的资产不清理

        private void Awake()
        {
            _config = Config != null ? Config : DemoConfig.CreateDefault();
            _ownsConfig = Config == null;

            BuildGround();
            var cam = EnsureCamera();
            var player = BuildPlayer(cam);
            foreach (var pos in EnemySpawns) BuildEnemy(pos);
            WireCamera(cam, player);
            BuildHUD();
            RegisterHitCue();
        }

        private void OnDestroy()
        {
            GameplayCueManager.Instance.OnGameplayCue -= OnCue;
            if (_ownsConfig && _config != null)
            {
                foreach (var a in _config.EnumerateSubAssets()) if (a != null) Destroy(a);
                Destroy(_config);
            }
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

            // ── 近战：两条判定（轻击 entry0 / 重击 entry1），攻击定义来自配置 ──
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = _config.LightAttack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.0f }
            });
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = _config.HeavyAttack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.1f }
            });

            // 技能（剑轻击 / 斧重击 / 远程 / 专注）—— GiveAbility 内部 Instantiate，不污染配置资产
            asc.GiveAbility(_config.MeleeAbility);
            asc.GiveAbility(_config.HeavyAbility);

            // 锁定系统（视角来源=相机）
            var targeting = p.AddComponent<TargetingSystemComponent>();
            targeting.ViewSource = cam.transform;

            // 远程：枪口 + 射击组件（子弹定义来自配置）
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(p.transform);
            muzzle.localPosition = new Vector3(0, 0.2f, 0.6f);

            var shooter = p.AddComponent<DemoRanged>();
            shooter.ASC = asc; shooter.Muzzle = muzzle; shooter.Bullet = _config.Bullet; shooter.Targeting = targeting;

            asc.GiveAbility(_config.RangedAbility);
            asc.GiveAbility(_config.FocusAbility);

            // 技能互斥规则（数据驱动）：专注 block 近战、远程 cancel 专注
            asc.SetInteractionRules(_config.InteractionRules);

            // ── 输入分发：InputSystemComponent + 控制集（来自配置）──
            var inputSys = p.AddComponent<InputSystemComponent>(); // Awake 自动找同物体 ASC
            inputSys.PushInputSetup(_config.CombatInput);

            // ── 两把武器（WeaponComponent 装备时注入武器标签）──
            var sword = MakeWeapon(p.transform, "Sword", DemoConfig.SwordTag);
            var axe = MakeWeapon(p.transform, "Axe", DemoConfig.AxeTag);

            var ctrl = p.AddComponent<DemoPlayerController>();
            ctrl.ASC = asc; ctrl.Controller = cc; ctrl.Melee = melee; ctrl.Targeting = targeting;
            ctrl.PowerBuff = _config.PowerBuff;
            ctrl.InputSystem = inputSys; ctrl.VehicleSetup = _config.VehicleInput;
            ctrl.Sword = sword; ctrl.Axe = axe;
            ctrl.MeleeInputTag = DemoConfig.InputMelee; ctrl.RangedInputTag = DemoConfig.InputRanged; ctrl.FocusInputTag = DemoConfig.InputFocus;
            ctrl.EquipSword(); // 默认装备剑（注入 Weapon.Sword → 近战键映射到轻击）

            PlayerASC = asc; Melee = melee; Controller = ctrl; Targeting = targeting; InputSystem = inputSys;
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
            asc.OnAttributeChanged += data =>
            {
                if (data.Attribute == health.HealthAttribute && data.NewValue < data.OldValue && !poiseComp.IsStaggered)
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
            hud.Controller = Controller;
        }

        // ---------- GameplayCue：命中火花 ----------
        private void RegisterHitCue() => GameplayCueManager.Instance.OnGameplayCue += OnCue;

        private void OnCue(GameObject target, GameplayTag cueTag, EGameplayCueEvent ev, GameplayCueParameters p)
        {
            if (ev == EGameplayCueEvent.Executed && cueTag.MatchesTag(DemoConfig.HitCue))
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

        // 造一把武器：子物体挂 WeaponComponent + 武器标签（装备时注入 owner ASC）
        private WeaponComponent MakeWeapon(Transform parent, string label, GameplayTag weaponTag)
        {
            var go = new GameObject("Weapon_" + label);
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeaponComponent>();
            w.WeaponTags.AddTag(weaponTag);
            return w;
        }

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
