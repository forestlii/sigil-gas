// Copyright 2026 Likeon All Rights Reserved.
// 等待某输入按下（带状态标签门控）。
// Unity 无 per-ability 输入绑定，故这里显式传入 inputTag，监听同物体上的 InputSystemComponent。

using System;

namespace Likeon.GAS
{
    /// <summary>
    /// 等待指定输入（带触发类型）按下，且 ASC 满足 RequiredTags、不含 IgnoredTags 时回调。
    /// 用于"按住蓄力松开触发""连段窗口再次按键"等。
    /// </summary>
    public class AbilityTask_WaitInputPress : AbilityTask
    {
        private GameplayTag _inputTag;
        private InputTriggerEvent _triggerEvent;
        private GameplayTagContainer _requiredTags;
        private GameplayTagContainer _ignoredTags;
        private bool _onlyTriggerOnce;
        private float _startTime;
        private InputSystemComponent _input;

        /// <summary>按下回调，参数 = 从 Activate 到按下的等待时长（秒）。(TimeWaited)。</summary>
        public event Action<float> OnPress;

        public static AbilityTask_WaitInputPress WaitInputPress(
            GameplayAbility ability, GameplayTag inputTag,
            GameplayTagContainer requiredTags = null, GameplayTagContainer ignoredTags = null,
            InputTriggerEvent triggerEvent = InputTriggerEvent.Started, bool onlyTriggerOnce = true)
        {
            var task = new AbilityTask_WaitInputPress
            {
                _inputTag = inputTag,
                _requiredTags = requiredTags,
                _ignoredTags = ignoredTags,
                _triggerEvent = triggerEvent,
                _onlyTriggerOnce = onlyTriggerOnce
            };
            task.InitTask(ability);
            return task;
        }

        protected override void OnActivate()
        {
            _startTime = UnityEngine.Time.time;
            if (ASC != null) _input = ASC.GetComponent<InputSystemComponent>();
            if (_input != null) _input.OnReceivedInput += HandleInput;
        }

        private void HandleInput(InputActionData data, GameplayTag tag, InputTriggerEvent evt)
        {
            if (!tag.Equals(_inputTag) || evt != _triggerEvent) return;

            if (_requiredTags != null)
                foreach (var t in _requiredTags)
                    if (!ASC.HasMatchingGameplayTag(t)) return;
            if (_ignoredTags != null)
                foreach (var t in _ignoredTags)
                    if (ASC.HasMatchingGameplayTag(t)) return;

            OnPress?.Invoke(UnityEngine.Time.time - _startTime);
            if (_onlyTriggerOnce) EndTask();
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (_input != null) _input.OnReceivedInput -= HandleInput;
        }
    }
}
