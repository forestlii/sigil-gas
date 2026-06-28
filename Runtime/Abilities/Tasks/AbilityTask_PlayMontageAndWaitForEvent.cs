// Copyright 2026 Likeon All Rights Reserved.
// 播放一段动画（Animator 状态）并等待其结束，同时监听匹配的 GameplayEvent。
// 连段/技能常用：边播挥砍动画边等命中事件或再次输入。
//
// 用 Animator.CrossFade 播状态 + 协程按 duration 计时驱动结束回调。
// 关键：animator 可选、duration 必填——实战传 Animator 播放；纯逻辑/测试可不传 animator，仅验计时/事件/取消。
// 用法：
//   var t = AbilityTask_PlayMontageAndWaitForEvent.PlayMontageAndWaitForEvent(this, animator, "Attack01", 0.8f, hitTags);
//   t.OnEventReceived += (tag, data) => { ...命中确认... };
//   t.OnCompleted     += () => { ...播完接下一段... };
//   t.Activate();

using System;
using System.Collections;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 播放 Animator 状态并等其按 <c>duration</c> 结束，期间监听匹配 <c>eventTags</c> 的 GameplayEvent。
    /// 回调：OnCompleted（播完）/ OnBlendOut（结束前 blendOutTime）/ OnInterrupted（动画被其它状态覆盖）/
    /// OnCancelled（任务/技能被取消）/ OnEventReceived（收到匹配事件，可多次）。
    /// </summary>
    public class AbilityTask_PlayMontageAndWaitForEvent : AbilityTask
    {
        private Animator _animator;                  // 可选：null 则仅按 duration 计时（纯逻辑/测试）
        private string _stateName;                   // Animator 状态名（CrossFade 目标）
        private float _duration;                     // 动画时长（秒，rate 前）
        private GameplayTagContainer _eventTags;     // 空/null = 匹配所有事件
        private float _rate;                         // 播放速率（实际时长 = duration / rate）
        private float _blendOutTime;                 // 结束前多少秒触发 OnBlendOut
        private float _crossFadeTime;                // CrossFade 过渡时长
        private int _layer;                          // Animator 层
        private bool _stopWhenAbilityEnds;

        /// <summary>收到匹配事件（事件 tag，负载）；可触发多次。</summary>
        public event Action<GameplayTag, GameplayEventData> OnEventReceived;
        /// <summary>动画完整播完。</summary>
        public event Action OnCompleted;
        /// <summary>动画临近结束开始混出（结束前 blendOutTime）。</summary>
        public event Action OnBlendOut;
        /// <summary>动画被其它状态覆盖打断（仅 animator 有效时检测）。</summary>
        public event Action OnInterrupted;
        /// <summary>任务被外部取消（技能结束/被打断）。</summary>
        public event Action OnCancelled;

        public static AbilityTask_PlayMontageAndWaitForEvent PlayMontageAndWaitForEvent(
            GameplayAbility ability, Animator animator, string stateName, float duration,
            GameplayTagContainer eventTags = null, float rate = 1f, float blendOutTime = 0f,
            float crossFadeTime = 0.1f, int layer = 0, bool stopWhenAbilityEnds = true)
        {
            var task = new AbilityTask_PlayMontageAndWaitForEvent
            {
                _animator = animator,
                _stateName = stateName,
                _duration = duration,
                _eventTags = eventTags,
                _rate = rate,
                _blendOutTime = blendOutTime,
                _crossFadeTime = crossFadeTime,
                _layer = layer,
                _stopWhenAbilityEnds = stopWhenAbilityEnds
            };
            task.InitTask(ability);
            return task;
        }

        protected override void OnActivate()
        {
            // 无效输入（无动画且无时长）→ 立即取消（无动画源即视为播放失败）
            if (_animator == null && _duration <= 0f)
            {
                OnCancelled?.Invoke();
                EndTask();
                return;
            }

            if (ASC != null) ASC.OnGameplayEvent += HandleEvent;

            if (_animator != null && !string.IsNullOrEmpty(_stateName))
            {
                if (_crossFadeTime > 0f) _animator.CrossFade(_stateName, _crossFadeTime, _layer);
                else _animator.Play(_stateName, _layer);
            }

            RunCoroutine(Track());
        }

        private IEnumerator Track()
        {
            float playDuration = _rate > 1e-4f ? _duration / _rate : _duration;
            int targetHash = string.IsNullOrEmpty(_stateName) ? 0 : Animator.StringToHash(_stateName);
            bool entered = false;        // 是否已进入目标 state（CrossFade 完成）
            bool blendOutFired = false;
            float elapsed = 0f;

            while (elapsed < playDuration)
            {
                yield return null;
                elapsed += Time.deltaTime;

                // 打断检测：进入目标 state 后，若当前 state 偏离且未在过渡，判为被覆盖打断
                if (_animator != null && targetHash != 0)
                {
                    var info = _animator.GetCurrentAnimatorStateInfo(_layer);
                    if (!entered && info.shortNameHash == targetHash) entered = true;
                    else if (entered && info.shortNameHash != targetHash && !_animator.IsInTransition(_layer))
                    {
                        OnInterrupted?.Invoke();
                        EndTask();
                        yield break;
                    }
                }

                if (!blendOutFired && _blendOutTime > 0f && elapsed >= playDuration - _blendOutTime)
                {
                    blendOutFired = true;
                    OnBlendOut?.Invoke();
                }
            }

            OnCompleted?.Invoke();
            EndTask();
        }

        private void HandleEvent(GameplayTag tag, GameplayEventData data)
        {
            bool match = _eventTags == null || _eventTags.IsEmpty || _eventTags.HasTag(tag);
            if (match) OnEventReceived?.Invoke(tag, data);
        }

        protected override void OnDestroy(bool abilityEnded)
        {
            if (ASC != null) ASC.OnGameplayEvent -= HandleEvent;
            // 被外部取消（技能结束/打断 → abilityEnded=true）→ OnCancelled。
            // 自然结束（OnCompleted/OnInterrupted 走 EndTask，abilityEnded=false）不在此触发。
            if (abilityEnded) OnCancelled?.Invoke();
            // 注：Unity Animator 无法精确"停单个动画"；stopWhenAbilityEnds 的实际停止
            // 交由宿主 Animator 状态机过渡处理（如技能结束标签驱动回 Idle）。此处仅解绑 + 通知取消。
        }
    }
}
