// PlayableDemo：可玩的"功能展示场"引导组件，演示框架已实现的多条战斗线——
//   近战命中→扣血→Cue   远程子弹   锁定切换   削韧破防   buff 叠层   第三人称相机 + 血条 + HUD，
//   以及 输入分发(键→tag→技能) / 载具切换 / 技能 block-cancel / 武器换技能。
//
// 两种运行形态：
//  · prefab 模式（推荐，策划工作流）：场景里摆好 Player/Enemy prefab 实例（ASC.initialLoadouts 配技能/属性集），
//    PlayableDemo 只做"薄编排"——接 prefab 接不了的跨边界引用（相机 ViewSource / ThirdPersonCamera / HUD）和动态订阅
//    （敌人受击变色 / 命中 Cue 火花）。用菜单 Sigil ▸ GAS ▸ Demo ▸ Build All 生成 prefab+场景。
//  · 运行时回退（headless / 裸挂）：场景里没摆 prefab 实例时，PlayableDemo 用 DemoActorBuilder 在 Awake 现场构建一套
//    （供"挂上就跑"和冒烟测试）。结构构建与 prefab 共用 DemoActorBuilder，避免两处漂移。
//
// 数据驱动：可配数据（输入控制集 / 技能互斥 / 技能 / 攻击 / 子弹 / 效果）来自 Config(DemoConfig 资产)；留空则 CreateDefault() 内存建默认。
// 注：本 demo 只把核心包里"已实现并测试过"的功能调用出来给人看，不含战斗逻辑本身。
using System.Collections;
using System.Collections.Generic;
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class PlayableDemo : MonoBehaviour
    {
        [Header("数据驱动配置（拖一个 DemoConfig.asset；留空=用代码默认）")]
        [Tooltip("策划在此资产里配输入分发/技能互斥/技能/攻击/效果。用菜单 Sigil ▸ GAS ▸ Generate Demo Config Assets 可一键生成一套并接好。")]
        public DemoConfig Config;

        [Header("prefab 模式：场景里已摆好的 prefab 实例（留空=运行时回退现场构建）")]
        [Tooltip("把场景里的玩家 prefab 实例（DemoPlayer）拖到这里；留空则 PlayableDemo 用 DemoActorBuilder 现场构建（headless/裸挂）。")]
        public DemoPlayerController ScenePlayer;
        [Tooltip("把场景里的敌人 prefab 实例（DemoEnemy 的 ASC）拖到这里；留空则现场构建。")]
        public List<AbilitySystemComponent> SceneEnemies = new List<AbilitySystemComponent>();

        // 敌人摆位（运行时回退构建时用；前方扇形便于演示锁定左右切换）
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

        private static readonly Color StaggerColor = new Color(0.95f, 0.85f, 0.35f);

        private DemoConfig _config;
        private bool _ownsConfig; // 默认配置由本组件 new 出来 → 销毁时负责清理；拖进来的资产不清理

        private void Awake()
        {
            _config = Config != null ? Config : DemoConfig.CreateDefault();
            _ownsConfig = Config == null;

            BuildGround();
            var cam = EnsureCamera();

            // 玩家：场景已摆 prefab 实例则薄编排接管，否则运行时回退现场构建
            GameObject player = ScenePlayer != null ? AdoptScenePlayer(cam) : BuildPlayer(cam);

            // 敌人：同上
            if (SceneEnemies != null && SceneEnemies.Count > 0)
                foreach (var e in SceneEnemies) { if (e != null) AdoptSceneEnemy(e); }
            else
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
            DemoActorBuilder.SetColor(g, new Color(0.24f, 0.26f, 0.30f));
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

        // ---------- 玩家 ----------
        // 运行时回退：用共享构造器现场建玩家结构，再接跨边界引用 + 授予 loadout。
        private GameObject BuildPlayer(Camera cam)
        {
            var refs = DemoActorBuilder.BuildPlayer(_config);
            refs.Root.transform.SetParent(transform, true); // 挂到 host 下，便于测试销毁清理

            // 授予技能/属性集（回退路径显式 GrantLoadout；prefab 模式由 initialLoadouts 在实例化 Awake 授予）
            refs.ASC.GrantLoadout(_config.BuildPlayerLoadout());

            // 跨边界引用：锁定视角来源 = 相机
            refs.Targeting.ViewSource = cam.transform;
            // 默认装备剑（注入 Weapon.Sword → 近战键映射到轻击）
            refs.PlayerController.EquipSword();

            CapturePlayer(refs.ASC, refs.Melee, refs.PlayerController, refs.Targeting, refs.InputSystem);
            return refs.Root;
        }

        // prefab 模式：场景里已摆好玩家实例（结构/技能由 prefab+initialLoadouts 提供），PlayableDemo 只接跨边界引用（相机）+ 运行时上色。
        private GameObject AdoptScenePlayer(Camera cam)
        {
            var ctrl = ScenePlayer;
            DemoActorBuilder.SetColor(ctrl.gameObject, DemoActorBuilder.PlayerColor); // prefab 未烘色，运行时上
            ctrl.Targeting.ViewSource = cam.transform;
            ctrl.EquipSword();
            CapturePlayer(ctrl.ASC, ctrl.Melee, ctrl, ctrl.Targeting, ctrl.InputSystem);
            return ctrl.gameObject;
        }

        private void CapturePlayer(AbilitySystemComponent asc, MeleeAttackTrace melee, DemoPlayerController ctrl,
            TargetingSystemComponent targeting, InputSystemComponent inputSys)
        {
            PlayerASC = asc; Melee = melee; Controller = ctrl; Targeting = targeting; InputSystem = inputSys;
        }

        // ---------- 敌人 ----------
        // 运行时回退：现场建敌人结构 + 授予 loadout + 接动态订阅。
        private void BuildEnemy(Vector3 pos)
        {
            var e = DemoActorBuilder.BuildEnemy(_config);
            e.transform.position = pos;
            e.transform.SetParent(transform, true);

            var asc = e.GetComponent<AbilitySystemComponent>();
            asc.GrantLoadout(_config.BuildEnemyLoadout());
            WireEnemyFeedback(asc);
            Enemies.Add(asc);
        }

        // prefab 模式：场景里已摆好敌人实例（属性集由 initialLoadouts 提供），只接动态订阅 + 运行时上色。
        private void AdoptSceneEnemy(AbilitySystemComponent asc)
        {
            DemoActorBuilder.SetColor(asc.gameObject, DemoActorBuilder.EnemyColor); // prefab 未烘色，运行时上
            WireEnemyFeedback(asc);
            Enemies.Add(asc);
        }

        // 敌人受击/破防的动态视觉反馈（事件订阅 + 血条）——跨边界/动态，prefab 接不了，运行时接。
        // 惰性取属性集：在回调里再 GetAttributeSet，不依赖 ASC.Awake 与 PlayableDemo.Awake 的先后（adopt 模式顺序不定）。
        private void WireEnemyFeedback(AbilitySystemComponent asc)
        {
            var poiseComp = asc.GetComponent<PoiseComponent>();
            var rend = asc.GetComponent<Renderer>();

            var bar = new GameObject("EnemyHealthBar").AddComponent<DemoHealthBar>();
            bar.Init(asc);

            asc.OnAttributeChanged += data =>
            {
                var health = asc.GetAttributeSet<AS_Health>();
                if (health != null && data.Attribute == health.HealthAttribute && data.NewValue < data.OldValue
                    && (poiseComp == null || !poiseComp.IsStaggered))
                    StartCoroutine(FlashRed(rend));
            };
            if (poiseComp != null)
            {
                poiseComp.OnPoiseBroken += () => DemoActorBuilder.SetColor(rend, StaggerColor);
                poiseComp.OnPoiseRecovered += () => DemoActorBuilder.SetColor(rend, DemoActorBuilder.EnemyColor);
            }
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
            DemoActorBuilder.SetColor(s, new Color(1f, 0.85f, 0.2f));
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
            DemoActorBuilder.SetColor(rend, Color.red);
            yield return new WaitForSeconds(0.1f);
            if (rend != null) DemoActorBuilder.SetColor(rend, DemoActorBuilder.EnemyColor);
        }
    }
}
