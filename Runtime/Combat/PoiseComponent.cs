// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 削韧机制组件（破防 → 硬直 → 恢复）。框架侧标准的削韧消费者，不绑定任何具体属性集：
//   - 监听削韧属性归零 → 破防：挂硬直标签(+可选硬直效果)，触发 OnPoiseBroken；
//   - 硬直持续 staggerDuration 后 → 复位：解硬直标签、削韧回满，触发 OnPoiseRecovered；
//   - 未硬直时，按恢复速率属性（每秒）恢复削韧（受削后等 recoverDelay 再恢复）。
// 属性按名解析（默认 Poise / MaxPoise / PoiseRecover）：配任何含这些属性名的属性集即可（含编辑器生成的）。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Poise Component")]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public class PoiseComponent : MonoBehaviour
    {
        [SerializeField] private AbilitySystemComponent abilitySystem;

        [Header("削韧属性名（跨属性集按名解析）")]
        [SerializeField] private string poiseName = "Poise";
        [SerializeField] private string maxPoiseName = "MaxPoise";
        [SerializeField] private string poiseRecoverName = "PoiseRecover";

        [Header("破防 / 硬直")]
        [Tooltip("破防时挂在身上的硬直状态标签（留空默认 State.Staggered）")]
        [SerializeField] private GameplayTag staggeredTag;
        [Tooltip("破防时施加的效果（可选，如硬直期间减速/禁手）")]
        [SerializeField] private GameplayEffect staggerEffect;
        [Tooltip("硬直持续秒数，之后解除并把削韧回满")]
        [SerializeField] private float staggerDuration = 1f;

        [Header("恢复")]
        [Tooltip("受削后多久开始恢复削韧（秒）")]
        [SerializeField] private float recoverDelay = 0.5f;

        /// <summary>破防（削韧归零）时触发。</summary>
        public event Action OnPoiseBroken;
        /// <summary>硬直结束、削韧复位时触发。</summary>
        public event Action OnPoiseRecovered;

        /// <summary>当前是否处于硬直。</summary>
        public bool IsStaggered { get; private set; }

        /// <summary>硬直持续秒数（运行时可调）。</summary>
        public float StaggerDuration { get => staggerDuration; set => staggerDuration = value; }
        /// <summary>受削后恢复延迟秒数（运行时可调）。</summary>
        public float RecoverDelay { get => recoverDelay; set => recoverDelay = value; }
        /// <summary>硬直状态标签。</summary>
        public GameplayTag StaggeredTag => staggeredTag;

        private GameplayAttribute _poiseAttr;
        private bool _poiseResolved;
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

        // 削韧属性句柄按名解析（属性集在 Awake 授予，故惰性解析并缓存）。
        private GameplayAttribute PoiseAttr()
        {
            if (!_poiseResolved && abilitySystem != null)
            {
                _poiseAttr = abilitySystem.FindAttributeByName(poiseName);
                _poiseResolved = _poiseAttr.IsValid;
            }
            return _poiseAttr;
        }

        private void HandleAttributeChanged(AttributeChangeData data)
        {
            var poiseAttr = PoiseAttr();
            if (!poiseAttr.IsValid || data.Attribute != poiseAttr) return;

            // 受削 → 重置恢复延迟
            if (data.NewValue < data.OldValue) _recoverDelayTimer = recoverDelay;
            // 归零 → 破防
            if (data.NewValue <= 0f && !IsStaggered) Break();
        }

        private void Update()
        {
            if (abilitySystem == null) return;

            if (IsStaggered)
            {
                _staggerTimer -= Time.deltaTime;
                if (_staggerTimer <= 0f) Recover();
                return;
            }

            var poiseData = abilitySystem.GetAttributeDataByName(poiseName);
            if (poiseData == null) return; // 没有削韧属性 → 组件不生效

            // 未硬直：延迟后按恢复速率恢复
            if (_recoverDelayTimer > 0f) { _recoverDelayTimer -= Time.deltaTime; return; }

            float cur = poiseData.CurrentValue;
            float max = abilitySystem.GetAttributeDataByName(maxPoiseName)?.CurrentValue ?? cur;
            if (cur < max)
            {
                float rate = abilitySystem.GetAttributeDataByName(poiseRecoverName)?.CurrentValue ?? 0f;
                float regen = rate * Time.deltaTime;
                if (regen > 0f)
                    abilitySystem.ApplyModToAttributeBase(PoiseAttr(), EAttributeModifierOp.Add, regen);
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

        /// <summary>硬直结束：解标签/效果，削韧回满，触发事件。</summary>
        private void Recover()
        {
            IsStaggered = false;
            abilitySystem.RemoveLooseGameplayTag(staggeredTag);
            if (_staggerEffectHandle.IsValid)
            {
                abilitySystem.RemoveActiveGameplayEffect(_staggerEffectHandle);
                _staggerEffectHandle = ActiveGameplayEffectHandle.Invalid;
            }
            var poiseAttr = PoiseAttr();
            float max = abilitySystem.GetAttributeDataByName(maxPoiseName)?.CurrentValue ?? 0f;
            if (poiseAttr.IsValid && max > 0f)
                abilitySystem.ApplyModToAttributeBase(poiseAttr, EAttributeModifierOp.Override, max);
            OnPoiseRecovered?.Invoke();
        }
    }
}
