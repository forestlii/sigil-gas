// PlayMode 冒烟测试：GASDemo 运行时构建整个功能展示场，验证各条战斗线在 demo 里被正确接通：
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

namespace Likeon.GAS.PlayTests
{
    public class DemoSmokeTest
    {
        private static readonly GameplayTag MeleeTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        private static readonly GameplayTag HeavyTag = GameplayTag.RequestTag("Ability.HeavyAttack");
        private static readonly GameplayTag FocusingTag = GameplayTag.RequestTag("State.Focusing");
        private static readonly GameplayTag HonkTag = GameplayTag.RequestTag("Event.Honk");

        private GASDemo.GASDemo BuildDemo(out GameObject host)
        {
            host = new GameObject("DemoHost");
            return host.AddComponent<GASDemo.GASDemo>(); // Awake 构建整个 demo
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
    }
}
