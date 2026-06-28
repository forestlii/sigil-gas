// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 等待计时/一帧。

using System;
using System.Collections;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>等待 duration 秒后回调一次。</summary>
    public class AbilityTask_WaitDelay : AbilityTask
    {
        private float _duration;

        /// <summary>计时结束回调。</summary>
        public event Action OnFinish;

        public static AbilityTask_WaitDelay WaitDelay(GameplayAbility ability, float duration)
        {
            var task = new AbilityTask_WaitDelay { _duration = Mathf.Max(0f, duration) };
            task.InitTask(ability);
            return task;
        }

        protected override void OnActivate() => RunCoroutine(Run());

        private IEnumerator Run()
        {
            if (_duration > 0f) yield return new WaitForSeconds(_duration);
            else yield return null;
            OnFinish?.Invoke();
            EndTask();
        }
    }

    /// <summary>等待一帧后回调一次。</summary>
    public class AbilityTask_WaitDelayOneFrame : AbilityTask
    {
        /// <summary>下一帧回调。</summary>
        public event Action OnFinish;

        public static AbilityTask_WaitDelayOneFrame WaitOneFrame(GameplayAbility ability)
        {
            var task = new AbilityTask_WaitDelayOneFrame();
            task.InitTask(ability);
            return task;
        }

        protected override void OnActivate() => RunCoroutine(Run());

        private IEnumerator Run()
        {
            yield return null;
            OnFinish?.Invoke();
            EndTask();
        }
    }
}
