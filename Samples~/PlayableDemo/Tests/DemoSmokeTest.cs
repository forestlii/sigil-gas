// PlayMode 冒烟测试：PlayableDemo 运行时构建整个功能展示场，验证各条战斗线在 demo 里被正确接通：
//  A) 建场景 → 近战 → 敌人扣血；
//  B) 锁定系统锁到敌人；
//  C) 远程子弹命中锁定目标扣血；
//  D) Power buff 叠层（R 键效果）。
// 注：本测试验证的是"demo 是否正确调用了框架功能"（接线层），框架功能本身另有各自的专项测试。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.GAS;
using Likeon.GAS.Sample.PlayableDemo;

namespace Likeon.GAS.PlayTests
{
    public class DemoSmokeTest
    {
        private static readonly GameplayTag MeleeTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        private static readonly GameplayTag HeavyTag = GameplayTag.RequestTag("Ability.HeavyAttack");
        private static readonly GameplayTag FocusingTag = GameplayTag.RequestTag("State.Focusing");
        private static readonly GameplayTag HonkTag = GameplayTag.RequestTag("Event.Honk");

        private PlayableDemo BuildDemo(out GameObject host)
        {
            host = new GameObject("DemoHost");
            return host.AddComponent<PlayableDemo>(); // Awake 构建整个 demo
        }

        // ============ A) 建场景 + 近战扣血 ============
        [UnityTest]
        public IEnumerator A_Demo_Builds_And_MeleeDealsDamage()
        {
            var demo = BuildDemo(out var host);
            yield return null; // 等 Awake/Start

            Assert.IsNotNull(demo.PlayerASC, "玩家 ASC 应已构建");
            Assert.Greater(demo.Enemies.Count, 0, "应已构建敌人");
            Assert.IsNotNull(demo.Melee, "近战判定应已构建");
            Assert.IsNotNull(demo.Controller, "玩家控制器应已构建");
            Assert.IsNotNull(demo.Targeting, "锁定系统应已构建");

            var enemy = demo.Enemies[0];
            var enemyHealth = enemy.GetAttributeSet<AS_Health>();
            Assert.IsNotNull(enemyHealth);

            // 把敌人移到武器判定点附近（玩家在 0,1,0，socket 在前方 +1.2）
            enemy.transform.position = new Vector3(0, 1, 1.2f);
            yield return new WaitForFixedUpdate();

            float before = enemyHealth.Health.CurrentValue;
            demo.Controller.TryAttack();            // 激活近战技能 → 开判定窗口
            yield return new WaitForSeconds(0.4f);  // 判定窗口期 + 关闭

            Assert.Less(enemyHealth.Health.CurrentValue, before, "demo 近战应对敌人造成伤害");

            Object.Destroy(host);
            yield return null;
        }

        // ============ B) 锁定系统锁到敌人 ============
        [UnityTest]
        public IEnumerator B_LockOn_LocksEnemy()
        {
            var demo = BuildDemo(out var host);
            yield return null;
            yield return new WaitForFixedUpdate();

            demo.Targeting.MaxViewAngle = 180f; // 去掉视角锥对 headless 相机朝向的依赖，专测锁定接线
            demo.Targeting.ToggleLock();

            Assert.IsTrue(demo.Targeting.IsLockedOn, "应锁定到目标");
            Assert.IsTrue(demo.Enemies.Exists(e => e.gameObject == demo.Targeting.TargetedActor),
                "锁定目标应是其中一个敌人");

            Object.Destroy(host);
            yield return null;
        }

        // ============ C) 远程子弹命中锁定目标扣血 ============
        [UnityTest]
        public IEnumerator C_Ranged_HitsLockedTarget()
        {
            var demo = BuildDemo(out var host);
            yield return null;
            yield return new WaitForFixedUpdate();

            demo.Targeting.MaxViewAngle = 180f;
            demo.Targeting.ToggleLock();
            Assert.IsTrue(demo.Targeting.IsLockedOn, "远程测试前应先锁定");

            var target = demo.Targeting.TargetedActor.GetComponent<AbilitySystemComponent>();
            var hp = target.GetAttributeSet<AS_Health>();
            float before = hp.Health.CurrentValue;

            demo.Controller.TryRanged();            // 激活远程技能 → 朝锁定目标发射子弹
            yield return new WaitForSeconds(0.6f);  // 子弹飞行 + 命中

            Assert.Less(hp.Health.CurrentValue, before, "远程子弹应命中锁定目标并扣血");

            Object.Destroy(host);
            yield return null;
        }

        // ============ D) Power buff 叠层 ============
        [UnityTest]
        public IEnumerator D_PowerBuff_Stacks()
        {
            var demo = BuildDemo(out var host);
            yield return null;

            var asc = demo.PlayerASC;
            var buff = demo.Controller.PowerBuff;
            Assert.IsNotNull(buff, "Power buff 应已配置");

            asc.ApplyGameplayEffectToSelf(buff); // 第 1 层
            asc.ApplyGameplayEffectToSelf(buff); // 第 2 层（同组合并）

            Assert.AreEqual(1, asc.GetActiveGameplayEffects().Count, "应合并为 1 个激活实例");
            Assert.AreEqual(2, asc.GetActiveGameplayEffects()[0].StackCount, "层数应为 2");

            Object.Destroy(host);
            yield return null;
        }

        // ============ E) 输入分发：经 ReceiveInput 走完整分发链激活近战 ============
        [UnityTest]
        public IEnumerator E_InputDispatch_MeleeViaReceiveInput_Damages()
        {
            var demo = BuildDemo(out var host);
            yield return null;
            yield return new WaitForFixedUpdate();

            var enemy = demo.Enemies[0];
            var hp = enemy.GetAttributeSet<AS_Health>();
            enemy.transform.position = new Vector3(0, 1, 1.2f);
            yield return new WaitForFixedUpdate();

            float before = hp.Health.CurrentValue;
            // 直接喂输入：InputTag.Melee → 控制集处理器 → 激活近战技能（不走 TryActivate 直调）
            demo.InputSystem.ReceiveInput(demo.Controller.MeleeInputTag, InputTriggerEvent.Started, InputActionData.Empty);
            yield return new WaitForSeconds(0.4f);

            Assert.Less(hp.Health.CurrentValue, before, "经输入分发激活的近战应对敌人造成伤害");

            Object.Destroy(host);
            yield return null;
        }

        // ============ F) 武器→不同技能：同一近战键，剑激活轻击 / 斧激活重击 ============
        [UnityTest]
        public IEnumerator F_WeaponSwitch_ChangesActivatedMeleeAbility()
        {
            var demo = BuildDemo(out var host);
            yield return null;

            GameplayAbility activated = null;
            demo.PlayerASC.OnAbilityActivated += a => activated = a;
            var meleeKey = demo.Controller.MeleeInputTag;

            // 默认剑 → 近战键激活轻击 Ability.MeleeAttack
            demo.Controller.EquipSword();
            activated = null;
            demo.InputSystem.ReceiveInput(meleeKey, InputTriggerEvent.Started, InputActionData.Empty);
            Assert.IsNotNull(activated, "剑装备下近战键应激活一个技能");
            Assert.IsTrue(activated.GetAbilityTags().HasTag(MeleeTag), "剑 → 应激活轻击 MeleeAttack");

            // 切斧 → 同一近战键改激活重击 Ability.HeavyAttack
            demo.Controller.EquipAxe();
            activated = null;
            demo.InputSystem.ReceiveInput(meleeKey, InputTriggerEvent.Started, InputActionData.Empty);
            Assert.IsNotNull(activated, "斧装备下近战键应激活一个技能");
            Assert.IsTrue(activated.GetAbilityTags().HasTag(HeavyTag), "斧 → 应激活重击 HeavyAttack");

            Object.Destroy(host);
            yield return null;
        }

        // ============ G) block：专注期间近战被交互规则挡住 ============
        [UnityTest]
        public IEnumerator G_Focus_BlocksMelee()
        {
            var demo = BuildDemo(out var host);
            yield return null;
            yield return new WaitForFixedUpdate();

            var enemy = demo.Enemies[0];
            var hp = enemy.GetAttributeSet<AS_Health>();
            enemy.transform.position = new Vector3(0, 1, 1.2f);
            yield return new WaitForFixedUpdate();

            demo.Controller.Focus(); // 激活专注 → 挂 State.Focusing
            Assert.IsTrue(demo.PlayerASC.HasMatchingGameplayTag(FocusingTag), "专注应激活并挂 State.Focusing");

            float before = hp.Health.CurrentValue;
            demo.InputSystem.ReceiveInput(demo.Controller.MeleeInputTag, InputTriggerEvent.Started, InputActionData.Empty);
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(before, hp.Health.CurrentValue, "专注期间近战被 block，敌人不应掉血");

            Object.Destroy(host);
            yield return null;
        }

        // ============ H) cancel：开火远程取消专注 ============
        [UnityTest]
        public IEnumerator H_Ranged_CancelsFocus()
        {
            var demo = BuildDemo(out var host);
            yield return null;

            demo.Controller.Focus();
            Assert.IsTrue(demo.PlayerASC.HasMatchingGameplayTag(FocusingTag), "专注应先激活");

            // 远程激活 → 交互规则 AbilityTagsToCancel 取消专注
            demo.InputSystem.ReceiveInput(demo.Controller.RangedInputTag, InputTriggerEvent.Started, InputActionData.Empty);

            Assert.IsFalse(demo.PlayerASC.HasMatchingGameplayTag(FocusingTag), "开火远程应取消专注（State.Focusing 移除）");

            Object.Destroy(host);
            yield return null;
        }

        // ============ I) 载具切换：载具模式下近战键改鸣笛、不攻击 ============
        [UnityTest]
        public IEnumerator I_Vehicle_MeleeHonks_NotAttack()
        {
            var demo = BuildDemo(out var host);
            yield return null;
            yield return new WaitForFixedUpdate();

            var enemy = demo.Enemies[0];
            var hp = enemy.GetAttributeSet<AS_Health>();
            enemy.transform.position = new Vector3(0, 1, 1.2f);
            yield return new WaitForFixedUpdate();

            bool honked = false;
            demo.PlayerASC.OnGameplayEvent += (tag, data) => { if (tag.MatchesTag(HonkTag)) honked = true; };

            demo.Controller.ToggleVehicle(); // 压入载具控制集
            Assert.IsTrue(demo.Controller.InVehicle, "应进入载具模式");

            float before = hp.Health.CurrentValue;
            demo.InputSystem.ReceiveInput(demo.Controller.MeleeInputTag, InputTriggerEvent.Started, InputActionData.Empty);
            yield return new WaitForSeconds(0.4f);

            Assert.IsTrue(honked, "载具模式下近战键应广播 Event.Honk（鸣笛）");
            Assert.AreEqual(before, hp.Health.CurrentValue, "载具模式下近战键不应攻击敌人");

            Object.Destroy(host);
            yield return null;
        }

        // ============ J) 数据驱动：DemoConfig 默认工厂产出完整且交叉引用接好 ============
        [Test]
        public void J_DemoConfig_CreateDefault_IsComplete()
        {
            var cfg = DemoConfig.CreateDefault();
            Assert.IsNotNull(cfg.CombatInput, "CombatInput");
            Assert.IsNotNull(cfg.VehicleInput, "VehicleInput");
            Assert.IsNotNull(cfg.InteractionRules, "InteractionRules");
            Assert.IsNotNull(cfg.MeleeAbility, "MeleeAbility");
            Assert.IsNotNull(cfg.HeavyAbility, "HeavyAbility");
            Assert.IsNotNull(cfg.RangedAbility, "RangedAbility");
            Assert.IsNotNull(cfg.FocusAbility, "FocusAbility");
            Assert.IsNotNull(cfg.LightAttack, "LightAttack");
            Assert.IsNotNull(cfg.HeavyAttack, "HeavyAttack");
            Assert.IsNotNull(cfg.Bullet, "Bullet");
            Assert.IsNotNull(cfg.PowerBuff, "PowerBuff");
            // 交叉引用接好
            Assert.IsNotNull(cfg.LightAttack.TargetEffect, "LightAttack.TargetEffect");
            Assert.IsNotNull(cfg.MeleeAbility.CostEffect, "MeleeAbility.CostEffect");
            Assert.IsNotNull(cfg.Bullet.Attack, "Bullet.Attack");

            foreach (var a in cfg.EnumerateSubAssets()) if (a != null) Object.DestroyImmediate(a);
            Object.DestroyImmediate(cfg);
        }

        // ============ K) 数据驱动：指定 Config（非回退）也能正确构建并近战生效 ============
        [UnityTest]
        public IEnumerator K_AssignedConfig_BuildsAndMeleeWorks()
        {
            var host = new GameObject("DemoHost");
            host.SetActive(false);                                 // 先停用，赶在 Awake 前赋 Config
            var demo = host.AddComponent<PlayableDemo>();
            demo.Config = DemoConfig.CreateDefault();      // 指定配置 → 走"用资产"路径（非回退）
            host.SetActive(true);                                  // 此时 Awake 用 assigned config 构建
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(demo.PlayerASC, "应用 assigned config 构建出玩家");
            Assert.AreSame(demo.Config, demo.ActiveConfig, "应使用 assigned config（非回退默认）");

            var enemy = demo.Enemies[0];
            var hp = enemy.GetAttributeSet<AS_Health>();
            enemy.transform.position = new Vector3(0, 1, 1.2f);
            yield return new WaitForFixedUpdate();

            float before = hp.Health.CurrentValue;
            demo.Controller.TryAttack();
            yield return new WaitForSeconds(0.4f);
            Assert.Less(hp.Health.CurrentValue, before, "用资产配置时近战仍应造成伤害");

            var cfg = demo.Config;
            Object.Destroy(host);
            yield return null;
            foreach (var a in cfg.EnumerateSubAssets()) if (a != null) Object.Destroy(a);
            Object.Destroy(cfg);
        }

        // ============ L) 数据驱动 Loadout：玩家/敌人装载产出正确的属性集 + 技能 ============
        // 验证 prefab 化路径的数据地基——ASC.initialLoadouts 用的就是这两个装载（Config 定义、Loadout 装载引用）。
        [UnityTest]
        public IEnumerator L_Loadouts_GrantCorrectAttributeSetsAndAbilities()
        {
            var cfg = DemoConfig.CreateDefault();

            // 玩家装载：AS_Health + AS_Stamina + 4 技能（近战/重击/远程/专注）
            var playerGo = new GameObject("LoadoutPlayer");
            var pasc = playerGo.AddComponent<AbilitySystemComponent>();
            yield return null;
            pasc.GrantLoadout(cfg.BuildPlayerLoadout());
            Assert.IsNotNull(pasc.GetAttributeSet<AS_Health>(), "玩家装载应含 AS_Health");
            Assert.IsNotNull(pasc.GetAttributeSet<AS_Stamina>(), "玩家装载应含 AS_Stamina");
            Assert.AreEqual(4, pasc.GetGrantedAbilities().Count, "玩家装载应授予 4 个技能");

            // 敌人装载：AS_Health + AS_Poise（削韧），无技能
            var enemyGo = new GameObject("LoadoutEnemy");
            var easc = enemyGo.AddComponent<AbilitySystemComponent>();
            yield return null;
            easc.GrantLoadout(cfg.BuildEnemyLoadout());
            Assert.IsNotNull(easc.GetAttributeSet<AS_Health>(), "敌人装载应含 AS_Health");
            Assert.IsNotNull(easc.GetAttributeSet<AS_Poise>(), "敌人装载应含 AS_Poise");
            Assert.AreEqual(0, easc.GetGrantedAbilities().Count, "敌人装载不授予技能");

            Object.Destroy(playerGo);
            Object.Destroy(enemyGo);
            yield return null;
            foreach (var a in cfg.EnumerateSubAssets()) if (a != null) Object.Destroy(a);
            Object.Destroy(cfg);
        }

        // ============ M) prefab 模式：实例化烘出的玩家 prefab → initialLoadouts 自动授予属性集+技能 ============
        // 验证 #7 prefab 化产物：Resources 里的 DemoPlayer.prefab 实例化后，靠 ASC.initialLoadouts 在 Awake 自动
        // 授予属性集/技能（无需代码 AddAttributeSet/GiveAbility），组件 + 输入控制集随 prefab 自带。
        [UnityTest]
        public IEnumerator M_PlayerPrefab_Instantiates_WithLoadoutGranted()
        {
            var prefab = Resources.Load<GameObject>("DemoPlayer");
            Assert.IsNotNull(prefab, "应能从 Resources 加载 DemoPlayer prefab（先运行 Sigil ▸ GAS ▸ Demo ▸ Build Prefabs 生成）");

            var player = Object.Instantiate(prefab);
            yield return null; // 等 Awake：ASC.initialLoadouts 授予

            var asc = player.GetComponent<AbilitySystemComponent>();
            Assert.IsNotNull(asc, "prefab 应含 ASC");
            Assert.IsNotNull(asc.GetAttributeSet<AS_Health>(), "initialLoadouts 应在 Awake 授予 AS_Health");
            Assert.IsNotNull(asc.GetAttributeSet<AS_Stamina>(), "initialLoadouts 应在 Awake 授予 AS_Stamina");
            Assert.AreEqual(4, asc.GetGrantedAbilities().Count, "initialLoadouts 应授予 4 个技能");
            Assert.IsNotNull(player.GetComponent<DemoPlayerController>(), "prefab 应含控制器");
            Assert.IsNotNull(player.GetComponent<DemoRanged>(), "prefab 应含远程组件");
            Assert.IsNotNull(player.GetComponent<MeleeAttackTrace>(), "prefab 应含近战判定");
            var inputSys = player.GetComponent<InputSystemComponent>();
            Assert.IsNotNull(inputSys, "prefab 应含输入分发组件");
            Assert.IsNotNull(inputSys.GetCurrentInputSetup(), "prefab 应随带战斗输入控制集（PushInputSetup 已序列化进 prefab）");

            Object.Destroy(player);
            yield return null;
        }

        // ============ N) prefab 模式：敌人 prefab 实例化 → initialLoadouts 授予 AS_Health/AS_Poise ============
        [UnityTest]
        public IEnumerator N_EnemyPrefab_Instantiates_WithLoadoutGranted()
        {
            var prefab = Resources.Load<GameObject>("DemoEnemy");
            Assert.IsNotNull(prefab, "应能从 Resources 加载 DemoEnemy prefab");

            var enemy = Object.Instantiate(prefab);
            yield return null;

            var asc = enemy.GetComponent<AbilitySystemComponent>();
            Assert.IsNotNull(asc, "敌人 prefab 应含 ASC");
            Assert.IsNotNull(asc.GetAttributeSet<AS_Health>(), "initialLoadouts 应授予 AS_Health");
            Assert.IsNotNull(asc.GetAttributeSet<AS_Poise>(), "initialLoadouts 应授予 AS_Poise（削韧）");
            Assert.IsNotNull(enemy.GetComponent<PoiseComponent>(), "敌人 prefab 应含削韧组件");

            Object.Destroy(enemy);
            yield return null;
        }

        // ============ O) adopt 模式：场景已摆 prefab 实例 → PlayableDemo 薄编排接管（不重复构建）+ 近战仍生效 ============
        // 验证 #7 阶段②c：BuildScene 摆好的 prefab 实例 + PlayableDemo.ScenePlayer/SceneEnemies → Awake 走 adopt 路径
        // （只接相机/动态订阅，结构/技能来自 prefab），demo 各战斗线照常。
        [UnityTest]
        public IEnumerator O_AdoptMode_WiresScenePrefabs_AndMeleeWorks()
        {
            var playerPrefab = Resources.Load<GameObject>("DemoPlayer");
            var enemyPrefab = Resources.Load<GameObject>("DemoEnemy");
            Assert.IsNotNull(playerPrefab, "应能加载 DemoPlayer prefab");
            Assert.IsNotNull(enemyPrefab, "应能加载 DemoEnemy prefab");

            var host = new GameObject("DemoHost");
            host.SetActive(false); // 先停用，赶在 Awake 前接好场景实例引用
            var demo = host.AddComponent<PlayableDemo>();

            var player = Object.Instantiate(playerPrefab);
            player.transform.position = new Vector3(0, 1, 0);
            demo.ScenePlayer = player.GetComponent<DemoPlayerController>();

            var enemy = Object.Instantiate(enemyPrefab);
            enemy.transform.position = new Vector3(0, 1, 1.2f); // 武器判定点附近
            demo.SceneEnemies = new System.Collections.Generic.List<AbilitySystemComponent> { enemy.GetComponent<AbilitySystemComponent>() };

            host.SetActive(true); // Awake → adopt 路径
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.AreSame(demo.ScenePlayer.ASC, demo.PlayerASC, "adopt 模式应采用场景玩家实例的 ASC（非现场新建）");
            Assert.AreEqual(1, demo.Enemies.Count, "adopt 模式应采用场景敌人实例");
            Assert.AreSame(enemy.GetComponent<AbilitySystemComponent>(), demo.Enemies[0], "敌人应是场景实例");

            var hp = enemy.GetComponent<AbilitySystemComponent>().GetAttributeSet<AS_Health>();
            Assert.IsNotNull(hp, "敌人 prefab 的 initialLoadouts 应已授予 AS_Health");
            float before = hp.Health.CurrentValue;
            demo.Controller.TryAttack(); // 经输入分发激活近战
            yield return new WaitForSeconds(0.4f);
            Assert.Less(hp.Health.CurrentValue, before, "adopt 模式下近战应对敌人造成伤害");

            Object.Destroy(host);
            Object.Destroy(player);
            Object.Destroy(enemy);
            yield return null;
        }
    }
}
