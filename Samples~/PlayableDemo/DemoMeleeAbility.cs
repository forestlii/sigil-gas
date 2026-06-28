// Demo 近战技能：激活时扣体力（CommitAbility）并开启判定窗口。
using Likeon.GAS;
using UnityEngine;

namespace GASDemo
{
    /// <summary>演示用近战技能：走完整 GAS 路径（消耗/冷却由 Cost/Cooldown 效果驱动），并开启 MeleeAttackTrace。</summary>
    public class DemoMeleeAbility : GameplayAbility
    {
        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            // 扣消耗 + 进冷却（若 CostEffect/CooldownEffect 配了的话）
            if (!CommitAbility())
            {
                EndAbility(true);
                return;
            }

            // 开启判定窗口（结束由控制器按时长关闭）
            var trace = ASC.GetComponent<MeleeAttackTrace>();
            if (trace != null) trace.BeginAttackTrace(0);

            EndAbility(); // 立即结束技能本体；判定窗口独立计时
        }
    }
}
