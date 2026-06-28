// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 战斗中枢：攻击结果登记、受击反应、动画播放。
// 诚实说明：这里是单机版。

using System;
using System.Collections;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Combat System Component")]
    public class CombatSystemComponent : MonoBehaviour
    {
        [Tooltip("驱动动画的 Animator（留空则自动查找）")]
        [SerializeField] private Animator animator;
        [Tooltip("本角色 ASC（留空则自动查找）")]
        [SerializeField] private AbilitySystemComponent abilitySystem;

        public Animator Animator => animator;
        public AbilitySystemComponent AbilitySystem => abilitySystem;

        /// <summary>受击时触发（本角色被命中）。</summary>
        public event Action<AttackResult> OnAttackResultReceived;
        /// <summary>命中他人时触发（本角色打中目标）。</summary>
        public event Action<AttackResult> OnDealtDamage;

        public AttackResult LastProcessedAttackResult { get; private set; }

        protected virtual void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystemComponent>();
        }

        /// <summary>登记一次受到的攻击结果，并触发受击反应。</summary>
        public virtual void RegisterAttackResult(AttackResult result)
        {
            if (result == null) return;
            LastProcessedAttackResult = result;
            OnAttackResultReceived?.Invoke(result);
            ApplyHitReaction(result);
        }

        /// <summary>本角色作为攻击方，通知打中了目标（表现/连段判断用）。</summary>
        public void NotifyDealtDamage(AttackResult result) => OnDealtDamage?.Invoke(result);

        /// <summary>受击反应：击退 + 顿帧（hit-stop）。的受击处理简化。</summary>
        protected virtual void ApplyHitReaction(AttackResult result)
        {
            // 击退：朝"远离命中来源"方向推
            var sourceObj = result.EffectContext?.Instigator;
            if (sourceObj != null)
            {
                Vector3 dir = (transform.position - sourceObj.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) dir.Normalize();

                if (TryGetComponent<CharacterController>(out var cc))
                    cc.Move(dir * 0.2f); // 简化：一次小位移；实际可走受击动画 RootMotion
                else if (TryGetComponent<Rigidbody>(out var rb))
                    rb.AddForce(dir * 5f, ForceMode.VelocityChange);
            }
        }

        /// <summary>顿帧：短暂把 Animator 放慢（打击感）。</summary>
        public void ApplyHitStop(float duration, float playRateFactor)
        {
            if (animator == null || duration <= 0f) return;
            StopCoroutine(nameof(HitStopRoutine));
            StartCoroutine(HitStopRoutine(duration, Mathf.Clamp(playRateFactor, 0.1f, 0.9f)));
        }

        private IEnumerator HitStopRoutine(float duration, float factor)
        {
            float original = animator.speed;
            animator.speed = original * factor;
            yield return new WaitForSeconds(duration);
            animator.speed = original;
        }

        /// <summary>播放一个能力动作的动画（Animator State / Trigger）。的简化。</summary>
        public void PlayAttackAction(in AbilityAction action)
        {
            if (animator == null) return;
            animator.speed = action.PlayRate <= 0f ? 1f : action.PlayRate;
            if (!string.IsNullOrEmpty(action.StateName))
                animator.CrossFadeInFixedTime(action.StateName, 0.1f, 0, action.StartTimeSeconds);
        }
    }
}
