// 共享构造器：把玩家/敌人的"可 prefab 化结构"（组件 + prefab 内部引用 + 资产引用）一处构建，
// 供运行时回退（PlayableDemo，Config==null 时）与 Editor prefab 生成器（DemoPrefabBuilder）共用，避免两处构造漂移。
//
// 边界（关键）：本构造器只接 **prefab 内部引用**（子物体 socket/muzzle/武器、同物体组件）和 **资产引用**（攻击/子弹）。
//   · 不接 **跨边界引用**（相机 ViewSource、运行时 ThirdPersonCameraBehavior、HUD）——prefab 不能引用场景对象，交调用方运行时接。
//   · 不授予 **技能 / 属性集**——由 ASC.initialLoadouts（prefab 模式，实例化时 Awake 授予）或调用方 GrantLoadout（运行时回退）负责。
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public static class DemoActorBuilder
    {
        public static readonly Color PlayerColor = new Color(0.22f, 0.5f, 0.9f);
        public static readonly Color EnemyColor = new Color(0.85f, 0.3f, 0.28f);

        /// <summary>玩家结构构建结果（供调用方接跨边界引用 + 读取关键组件）。</summary>
        public struct PlayerRefs
        {
            public GameObject Root;
            public AbilitySystemComponent ASC;
            public CharacterController Controller;
            public MeleeAttackTrace Melee;
            public TargetingSystemComponent Targeting;
            public InputSystemComponent InputSystem;
            public DemoRanged Shooter;
            public DemoPlayerController PlayerController;
        }

        /// <summary>
        /// 构建玩家结构（无相机依赖、无技能授予）。返回未挂父级的根物体 + 关键组件引用。
        /// 跨边界引用（Targeting.ViewSource / PlayerController.ThirdPersonCamera）留空，调用方运行时接。
        /// 技能/属性集走 loadout（本构造器不碰）。
        /// </summary>
        public static PlayerRefs BuildPlayer(DemoConfig cfg)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            p.name = "Player";
            p.transform.position = new Vector3(0, 1, 0);
            SetColor(p, PlayerColor);
            Object.DestroyImmediate(p.GetComponent<Collider>()); // 用 CharacterController 作碰撞（编辑器/运行时都即时销毁）

            var cc = p.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;

            var asc = p.AddComponent<AbilitySystemComponent>(); // 属性集/技能由 loadout 提供，不在此 AddAttributeSet

            p.AddComponent<CombatTeamAgent>().SetTeamId(0);
            p.AddComponent<CombatSystemComponent>();
            var melee = p.AddComponent<MeleeAttackTrace>();

            // 武器判定点（角色前方）—— prefab 内部子物体
            var socket = new GameObject("WeaponSocket").transform;
            socket.SetParent(p.transform);
            socket.localPosition = new Vector3(0, 0, 1.2f);

            // 近战两条判定（轻击 entry0 / 重击 entry1）；socket 是 prefab 内部引用
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = cfg.LightAttack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.0f }
            });
            melee.Entries.Add(new MeleeAttackTrace.AttackTraceEntry
            {
                Attack = cfg.HeavyAttack,
                Trace = new CollisionTraceDefinition { SocketTransforms = new List<Transform> { socket }, TraceRadius = 1.1f }
            });

            // 锁定系统（ViewSource 留空，调用方接相机）
            var targeting = p.AddComponent<TargetingSystemComponent>();

            // 远程：枪口子物体 + 射击组件（子弹定义=资产引用）
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(p.transform);
            muzzle.localPosition = new Vector3(0, 0.2f, 0.6f);

            var shooter = p.AddComponent<DemoRanged>();
            shooter.ASC = asc; shooter.Muzzle = muzzle; shooter.Bullet = cfg.Bullet; shooter.Targeting = targeting;

            // 技能互斥规则（资产引用）
            asc.SetInteractionRules(cfg.InteractionRules);

            // 输入分发：InputSystemComponent + 控制集（资产引用）
            var inputSys = p.AddComponent<InputSystemComponent>();
            inputSys.PushInputSetup(cfg.CombatInput);

            // 两把武器（子物体 + 武器标签）
            var sword = MakeWeapon(p.transform, "Sword", DemoConfig.SwordTag);
            var axe = MakeWeapon(p.transform, "Axe", DemoConfig.AxeTag);

            // 控制器：接 prefab 内部引用（相机引用 ThirdPersonCamera 留空，调用方接）
            var ctrl = p.AddComponent<DemoPlayerController>();
            ctrl.ASC = asc; ctrl.Controller = cc; ctrl.Melee = melee; ctrl.Targeting = targeting;
            ctrl.PowerBuff = cfg.PowerBuff;
            ctrl.InputSystem = inputSys; ctrl.VehicleSetup = cfg.VehicleInput;
            ctrl.Sword = sword; ctrl.Axe = axe;
            ctrl.MeleeInputTag = DemoConfig.InputMelee; ctrl.RangedInputTag = DemoConfig.InputRanged; ctrl.FocusInputTag = DemoConfig.InputFocus;

            return new PlayerRefs
            {
                Root = p, ASC = asc, Controller = cc, Melee = melee, Targeting = targeting,
                InputSystem = inputSys, Shooter = shooter, PlayerController = ctrl
            };
        }

        /// <summary>构建敌人结构（无技能授予；属性集 AS_Health/AS_Poise 走 loadout）。返回未挂父级的根物体。</summary>
        public static GameObject BuildEnemy(DemoConfig cfg)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            e.name = "Enemy";
            SetColor(e, EnemyColor); // 保留 CapsuleCollider 供命中 + 锁定候选

            e.AddComponent<AbilitySystemComponent>(); // 属性集走 loadout（AS_Health/AS_Poise）
            e.AddComponent<CombatSystemComponent>();
            e.AddComponent<CombatTeamAgent>().SetTeamId(1);
            e.AddComponent<PoiseComponent>(); // 削韧机制（破防→硬直→恢复）
            return e;
        }

        // 造一把武器：子物体挂 WeaponComponent + 武器标签（装备时注入 owner ASC）
        public static WeaponComponent MakeWeapon(Transform parent, string label, GameplayTag weaponTag)
        {
            var go = new GameObject("Weapon_" + label);
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeaponComponent>();
            w.WeaponTags.AddTag(weaponTag);
            return w;
        }

        // URP/标准 通用着色。编辑器（烘 prefab）下跳过——renderer.material 在 edit 模式会实例化材质污染 prefab；
        // 颜色在运行时上（fallback 构建走 isPlaying；prefab adopt 模式由 PlayableDemo 运行时再上色）。
        public static void SetColor(GameObject go, Color c) => SetColor(go.GetComponent<Renderer>(), c);
        public static void SetColor(Renderer rend, Color c)
        {
            if (rend == null || !Application.isPlaying) return;
            var m = rend.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.color = c;
        }
    }
}
