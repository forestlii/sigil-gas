// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 等待匹配某些 tag 的 GameplayEvent。
// 监听 ASC（或外部目标 ASC）的 OnGameplayEvent。连段确认 / 命中确认 / 动画事件派发常用。

using System;

namespace Likeon.GAS
{
    /// <summary>
    /// 等待与 EventTags 匹配（容器为空 = 匹配所有）的 GameplayEvent。
    /// OnlyTriggerOnce=true 时收到一次即结束，否则持续监听直到技能结束。
    /// </summary>
    public class AbilityTask_WaitGameplayEvent : AbilityTask
    {
        private GameplayTagContainer _eventTags;            // 空/null = 匹配所有
        private bool _onlyTriggerOnce;
        private AbilitySystemComponent _externalTarget;     // 为空则监听 owner 的 ASC

        /// <summary>收到匹配事件时回调（事件 tag，负载）。</summary>
        public event Action<GameplayTag, GameplayEventData> OnEventReceived;

        private AbilitySystemComponent TargetASC => _externalTarget != null ? _externalTarget : ASC;

        public static AbilityTask_WaitGameplayEvent WaitGameplayEvent(
            GameplayAbility ability, GameplayTagContainer eventTags,
            AbilitySystemComponent optionalExternalTarget = null, bool onlyTriggerOnce = false)
        {
            var task = new AbilityTask_WaitGameplayEvent
            {
                _eventTags = eventTags,
                _onlyTriggerOnce = onlyTriggerOnce,
                _externalTarget = optionalExternalTarget
            };
            task.InitTask(ability);
            return task;
        }

        /// <summary>单个 tag 的便捷重载。</summary>
        public static AbilityTask_WaitGameplayEvent WaitGameplayEvent(
            GameplayAbility ability, GameplayTag eventTag, bool onlyTriggerOnce = false)
        {
            var c = new GameplayTagContainer();
            c.AddTag(eventTag);
            return WaitGameplayEvent(ability, c, null, onlyTriggerOnce);
        }

        protected override void OnActivate()
        {
            var target = TargetASC;
            if (target != null) target.OnGameplayEvent += HandleEvent;
        }

        private void HandleEvent(GameplayTag tag, GameplayEventData data)
        {
            // 容器为空 = 匹配所有；否则按层级匹配（HasTag 含子级）
            bool match = _eventTags == null || _eventTags.IsEmpty || _eventTags.HasTag(tag);
            if (!match) return;

            OnEventReceived?.Invoke(tag, data);
            if (_onlyTriggerOnce) EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            var target = TargetASC;
            if (target != null) target.OnGameplayEvent -= HandleEvent;
        }
    }
}
