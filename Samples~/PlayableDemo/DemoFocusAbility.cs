// Demo 专注技能：一个"持续型"技能，激活期间给角色挂 State.Focusing（ActivationOwnedLooseTags，在 PlayableDemo 里配）。
// 配合 AbilityInteractionRules：专注期间近战被 block（AbilityTagsToBlock），开火远程会 cancel 专注（AbilityTagsToCancel）。
// 用 AbilityTask_WaitDelay 持续一段时间后自动结束；被取消时框架会自动撤掉 owned 标签并取消未结束的任务。
using Likeon.GAS;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoFocusAbility : GameplayAbility
    {
        [Tooltip("专注持续时长（秒）；期间近战被 block")]
        public float Duration = 1.5f;

        protected override void OnActivateAbility(GameplayEventData triggerData)
        {
            if (!CommitAbility()) { EndAbility(true); return; }

            // 持续 Duration 秒后自动结束（State.Focusing 由 ActivationOwnedLooseTags 在激活期间维持）。
            var wait = AbilityTask_WaitDelay.WaitDelay(this, Duration);
            wait.OnFinish += () => EndAbility();
            wait.Activate();
        }
    }
}
