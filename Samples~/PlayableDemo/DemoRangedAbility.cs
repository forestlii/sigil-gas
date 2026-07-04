// Demo 远程技能：走完整 GAS 路径——激活时扣体力(CommitAbility)，再让 DemoRanged 发射子弹。
// 与 DemoMeleeAbility 同构（技能负责消耗/冷却 + 触发组件干活），演示同一套技能框架既驱动近战也驱动远程。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoRangedAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            // 扣消耗 + 进冷却（若 Cost/Cooldown 效果配了的话）
            if (!CommitAbility())
            {
                EndAbility(true);
                return;
            }

            var shooter = ASC.GetComponent<DemoRanged>();
            if (shooter != null) shooter.Fire();

            EndAbility(); // 立即结束技能本体
        }
    }
}
