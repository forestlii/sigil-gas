// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 驱动一个 TargetActor 等待目标数据。
// 单机实现：不含网络复制/预测，保留"启动采集→拿到有效数据/取消"的核心流程。

using System;

namespace Likeon.GAS
{
    /// <summary>
    /// 驱动一个 <see cref="TargetActor"/> 采集目标：Instant 确认即时回 ValidData；
    /// UserConfirmed 则等技能后续调用 targetActor.ConfirmTargeting()。
    /// </summary>
    public class AbilityTask_WaitTargetData : AbilityTask
    {
        private TargetActor _targetActor;

        /// <summary>采集到有效目标数据时回调。</summary>
        public event Action<TargetDataHandle> OnValidData;
        /// <summary>采集被取消时回调。</summary>
        public event Action OnCancelled;

        public static AbilityTask_WaitTargetData WaitTargetData(GameplayAbility ability, TargetActor targetActor)
        {
            var task = new AbilityTask_WaitTargetData { _targetActor = targetActor };
            task.InitTask(ability);
            return task;
        }

        /// <summary>暴露内部 TargetActor，便于 UserConfirmed 时外部择机 ConfirmTargeting。</summary>
        public TargetActor TargetActor => _targetActor;

        protected override void OnActivate()
        {
            if (_targetActor == null) { EndTask(); return; }
            _targetActor.OnTargetDataReady += HandleData;
            _targetActor.OnCanceled += HandleCanceled;
            _targetActor.SourceActor ??= ASC != null ? ASC.gameObject : null;
            _targetActor.StartTargeting(Ability); // Instant 会在此内部直接 ConfirmTargeting
        }

        private void HandleData(TargetDataHandle data)
        {
            OnValidData?.Invoke(data);
            EndTask();
        }

        private void HandleCanceled()
        {
            OnCancelled?.Invoke();
            EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (_targetActor != null)
            {
                _targetActor.OnTargetDataReady -= HandleData;
                _targetActor.OnCanceled -= HandleCanceled;
                _targetActor.StopTargeting();
            }
        }
    }
}
