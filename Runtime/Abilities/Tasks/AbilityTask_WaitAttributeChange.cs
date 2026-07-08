// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 等待某个属性发生变化。监听 ASC 的 OnAttributeChanged。

using System;

namespace Likeon.GAS
{
    /// <summary>
    /// 等待指定属性的值发生变化（对齐 UE AbilityTask_WaitAttributeChange 的事件形态）——
    /// 把 ASC 的 <see cref="AbilitySystemComponent.OnAttributeChanged"/> 事件包装成技能内可等待的任务。
    /// OnlyTriggerOnce=true 时变化一次即结束；否则持续监听直到技能结束。
    /// 可选监听外部目标 ASC（如"等目标血量变化"）。
    /// </summary>
    public class AbilityTask_WaitAttributeChange : AbilityTask
    {
        private GameplayAttribute _attribute;
        private bool _onlyTriggerOnce;
        private AbilitySystemComponent _externalTarget;   // 为空则监听 owner 的 ASC

        /// <summary>属性变化时回调（载荷：属性 / 旧值 / 新值 / 来源）。</summary>
        public event Action<AttributeChangeData> OnChanged;

        private AbilitySystemComponent TargetASC => _externalTarget != null ? _externalTarget : ASC;

        public static AbilityTask_WaitAttributeChange WaitAttributeChange(
            GameplayAbility ability, GameplayAttribute attribute,
            AbilitySystemComponent optionalExternalTarget = null, bool onlyTriggerOnce = false)
        {
            var task = new AbilityTask_WaitAttributeChange
            {
                _attribute = attribute,
                _onlyTriggerOnce = onlyTriggerOnce,
                _externalTarget = optionalExternalTarget
            };
            task.InitTask(ability);
            return task;
        }

        protected override void OnActivate()
        {
            var target = TargetASC;
            if (target != null) target.OnAttributeChanged += HandleChange;
        }

        private void HandleChange(AttributeChangeData data)
        {
            if (!data.Attribute.Equals(_attribute)) return;   // 只关心指定属性
            OnChanged?.Invoke(data);
            if (_onlyTriggerOnce) EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            var target = TargetASC;
            if (target != null) target.OnAttributeChanged -= HandleChange;
        }
    }
}
