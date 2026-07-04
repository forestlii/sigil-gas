// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 削韧机制组件（破防 → 硬直 → 恢复）。
// 本组件把这套常见机制收进框架，作为 AS_Poise 的标准消费者（标注为源码外补充）：
//   - 监听 Poise 归零 → 破防：挂硬直标签(+可选硬直效果)，触发 OnPoiseBroken；
//   - 硬直持续 staggerDuration 后 → 复位：解硬直标签、Poise 回满，触发 OnPoiseRecovered；
//   - 未硬直时，按 AS_Poise.PoiseRecover 的速率（每秒）恢复 Poise（受削后等 recoverDelay 再恢复）。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Poise Component")]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public class PoiseComponent : MonoBehaviour
    {
        [SerializeField] private AbilitySystemComponent abilitySystem;

        [Header("破防 / 硬直")]
        [Tooltip("破防时挂在身上的硬直状态标签（留空默认 State.Staggered）")]
        [SerializeField] private GameplayTag staggeredTag;
        [Tooltip("破防时施加的效果（可选，如硬直期间减速/禁手）")]
        [SerializeField] private GameplayEffect staggerEffect;
        [Tooltip("硬直持续秒数，之后解除并把 Poise 回满")]
        [SerializeField] private float staggerDuration = 1f;

        [Header("恢复")]
        [Tooltip("受削后多久开始恢复 Poise（秒）")]
        [SerializeField] private float recoverDelay = 0.5f;

        /// <summary>破防（Poise 归零）时触发。</summary>
        public event Action OnPoiseBroken;
        /// <summary>硬直结束、Poise 复位时触发。</summary>
        public event Action OnPoiseRecovered;

        /// <summary>当前是否处于硬直。</summary>
        public bool IsStaggered { get; private set; }

        /// <summary>硬直持续秒数（运行时可调）。</summary>
        public float StaggerDuration { get => staggerDuration; set => staggerDuration = value; }
        /// <summary>受削后恢复延迟秒数（运行时可调）。</summary>
        public float RecoverDelay { get => recoverDelay; set => recoverDelay = value; }
        /// <summary>硬直状态标签。</summary>
        public GameplayTag StaggeredTag => staggeredTag;

        private AS_Poise _poise;
        private float _staggerTimer;
        private float _recoverDelayTimer;
        private ActiveGameplayEffectHandle _staggerEffectHandle = ActiveGameplayEffectHandle.Invalid;

        private void Awake()
        {
            if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystemComponent>();
            if (!staggeredTag.IsValid) staggeredTag = GameplayTag.RequestTag("State.Staggered");
        }

        private void OnEnable()
        {
            if (abilitySystem != null) abilitySystem.OnAttributeChanged += HandleAttributeChanged;
        }

        private void OnDisable()
        {
            if (abilitySystem != null) abilitySystem.OnAttributeChanged -= HandleAttributeChanged;
        }

        private AS_Poise ResolvePoise()
            => _poise ??= abilitySystem != null ? abilitySystem.GetAttributeSet<AS_Poise>() : null;

        private void HandleAttributeChanged(AttributeChangeData data)
        {
            var poise = ResolvePoise();
            if (poise == null || data.Attribute != poise.PoiseAttribute) return;

            // 受削 → 重置恢复延迟
            if (data.NewValue < data.OldValue) _recoverDelayTimer = recoverDelay;
            // 归零 → 破防
            if (data.NewValue <= 0f && !IsStaggered) Break();
        }

        private void Update()
        {
            var poise = ResolvePoise();
            if (poise == null) return;

            if (IsStaggered)
            {
                _staggerTimer -= Time.deltaTime;
                if (_staggerTimer <= 0f) Recover();
                return;
            }

            // 未硬直：延迟后按 PoiseRecover 速率恢复
            if (_recoverDelayTimer > 0f) { _recoverDelayTimer -= Time.deltaTime; return; }

            float cur = poise.Poise.CurrentValue;
            float max = poise.MaxPoise.CurrentValue;
            if (cur < max)
            {
                float regen = poise.PoiseRecover.CurrentValue * Time.deltaTime;
                if (regen > 0f)
                    abilitySystem.ApplyModToAttributeBase(poise.PoiseAttribute, EAttributeModifierOp.Add, regen);
            }
        }

        /// <summary>破防：挂硬直标签 + 可选效果，触发事件。</summary>
        private void Break()
        {
            IsStaggered = true;
            _staggerTimer = staggerDuration;
            abilitySystem.AddLooseGameplayTag(staggeredTag);
            if (staggerEffect != null)
                _staggerEffectHandle = abilitySystem.ApplyGameplayEffectToSelf(staggerEffect);
            OnPoiseBroken?.Invoke();
        }

        /// <summary>硬直结束：解标签/效果，Poise 回满，触发事件。</summary>
        private void Recover()
        {
            IsStaggered = false;
            abilitySystem.RemoveLooseGameplayTag(staggeredTag);
            if (_staggerEffectHandle.IsValid)
            {
                abilitySystem.RemoveActiveGameplayEffect(_staggerEffectHandle);
                _staggerEffectHandle = ActiveGameplayEffectHandle.Invalid;
            }
            var poise = ResolvePoise();
            if (poise != null)
                abilitySystem.ApplyModToAttributeBase(poise.PoiseAttribute, EAttributeModifierOp.Override, poise.MaxPoise.CurrentValue);
            OnPoiseRecovered?.Invoke();
        }
    }
}
