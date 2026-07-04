// Demo 近战技能：激活时扣体力（CommitAbility）、开启判定窗口，按时长用 AbilityTask_WaitDelay 自动关闭。
// 自闭合让它无论被"直接激活"还是"经输入分发激活"都能正确收尾。
// TraceEntryIndex 选用 MeleeAttackTrace.Entries 的哪一条——不同武器配不同攻击（轻击 / 重击）就靠它。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    /// <summary>演示用近战技能：走完整 GAS 路径（消耗/冷却由 Cost/Cooldown 效果驱动），并开启 MeleeAttackTrace。</summary>
    public class DemoMeleeAbility : GameplayAbility
    {
        [Tooltip("用 MeleeAttackTrace.Entries 的哪一条（不同武器=不同攻击定义）")]
        public int TraceEntryIndex = 0;
        [Tooltip("判定窗口时长（秒）")]
        public float TraceWindow = 0.3f;

        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            // 扣消耗 + 进冷却（若 CostEffect/CooldownEffect 配了的话）
            if (!CommitAbility())
            {
                EndAbility(true);
                return;
            }

            var trace = ASC.GetComponent<MeleeAttackTrace>();
            if (trace == null) { EndAbility(); return; }

            // 开判定窗口 → 等 TraceWindow → 关窗 + 结束技能（任务在技能结束时会被框架统一清理）
            trace.BeginAttackTrace(TraceEntryIndex);
            var wait = AbilityTask_WaitDelay.WaitDelay(this, TraceWindow);
            wait.OnFinish += () =>
            {
                trace.EndAttackTrace();
                EndAbility();
            };
            wait.Activate();
        }
    }
}
