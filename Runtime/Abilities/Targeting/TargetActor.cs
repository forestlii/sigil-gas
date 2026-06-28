// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 目标采集器。
// 单机实现：不含网络预测/准心/玩家视角/扩散(Spread)。
// 这里实现"可复用、可配置、做 trace 出 TargetData"的核心，不含 Actor 生命周期与网络部分，
// 改为轻量普通类，由 AbilityTask_WaitTargetData 驱动 StartTargeting→ConfirmTargeting。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>目标确认方式。</summary>
    public enum EGameplayTargetingConfirmation
    {
        /// <summary>StartTargeting 后立即采集并确认（瞄准即得）。</summary>
        Instant,
        /// <summary>等外部显式调用 ConfirmTargeting（玩家确认 / 自定义时机）。</summary>
        UserConfirmed
    }

    /// <summary>
    /// 目标采集器基类。配置起点/方向/最大距离/层掩码/最多命中数/过滤器，
    /// ConfirmTargeting 时执行 trace，产出 <see cref="TargetDataHandle"/> 并回调。
    /// 子类实现 <see cref="DoTrace"/>（射线 / 球扫）。
    /// </summary>
    public abstract class TargetActor
    {
        // ---- 配置 ----
        /// <summary>采集来源（用于忽略自身、取默认起点/方向）。</summary>
        public GameObject SourceActor;
        /// <summary>起点来源 Transform（为空则用 SourceActor 的 transform）。</summary>
        public Transform StartTransform;
        /// <summary>显式起点（设了 UseExplicitStart 才用）。</summary>
        public Vector3 ExplicitStart;
        public bool UseExplicitStart;
        /// <summary>显式方向（设了 UseExplicitDirection 才用，否则用 StartTransform.forward）。</summary>
        public Vector3 ExplicitDirection = Vector3.forward;
        public bool UseExplicitDirection;

        /// <summary>最大射程。</summary>
        public float MaxRange = 100f;
        /// <summary>碰撞层掩码（的简化）。</summary>
        public LayerMask LayerMask = ~0;
        /// <summary>每次 trace 返回的最多命中数；0 = 只返回终点。</summary>
        public int MaxHitResultsPerTrace = 1;
        /// <summary>命中过滤器：返回 false 的 actor 被剔除（如剔除友军/死亡目标）。</summary>
        public Func<GameObject, bool> Filter;

        public EGameplayTargetingConfirmation ConfirmationType = EGameplayTargetingConfirmation.Instant;

        // ---- 回调----
        public event Action<TargetDataHandle> OnTargetDataReady;
        public event Action OnCanceled;

        protected GameplayAbility OwningAbility { get; private set; }
        private bool _active;

        /// <summary>开始采集。Instant 确认方式会立即 Confirm。+ ConfirmTargeting。</summary>
        public virtual void StartTargeting(GameplayAbility ability)
        {
            OwningAbility = ability;
            _active = true;
            if (ConfirmationType == EGameplayTargetingConfirmation.Instant)
                ConfirmTargeting();
        }

        /// <summary>执行采集并把结果回调出去。</summary>
        public virtual void ConfirmTargeting()
        {
            if (!_active) return;
            var hits = PerformTrace();
            OnTargetDataReady?.Invoke(MakeTargetData(hits));
        }

        /// <summary>取消采集。</summary>
        public virtual void CancelTargeting()
        {
            if (!_active) return;
            _active = false;
            OnCanceled?.Invoke();
        }

        public virtual void StopTargeting() => _active = false;

        // ---- 内部 ----
        protected Vector3 GetStart()
        {
            if (UseExplicitStart) return ExplicitStart;
            var t = StartTransform != null ? StartTransform : (SourceActor != null ? SourceActor.transform : null);
            return t != null ? t.position : Vector3.zero;
        }

        protected Vector3 GetDirection()
        {
            if (UseExplicitDirection) return ExplicitDirection.sqrMagnitude > 0f ? ExplicitDirection.normalized : Vector3.forward;
            var t = StartTransform != null ? StartTransform : (SourceActor != null ? SourceActor.transform : null);
            return t != null ? t.forward : Vector3.forward;
        }

        /// <summary>执行一次 trace，按 MaxHitResults / 过滤器 / 忽略自身 整理结果。</summary>
        protected virtual List<TargetHitResult> PerformTrace()
        {
            var start = GetStart();
            var end = start + GetDirection() * MaxRange;

            var raw = new List<TargetHitResult>();
            DoTrace(raw, start, end);

            // 按距离排序、过滤
            raw.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            var filtered = new List<TargetHitResult>();
            foreach (var h in raw)
            {
                if (h.HitActor == null) continue;
                if (SourceActor != null && (h.HitActor == SourceActor || h.HitActor.transform.IsChildOf(SourceActor.transform))) continue;
                if (Filter != null && !Filter(h.HitActor)) continue;
                filtered.Add(h);
                if (MaxHitResultsPerTrace > 0 && filtered.Count >= MaxHitResultsPerTrace) break;
            }

            // 无有效命中 → 返回终点
            if (filtered.Count == 0)
                filtered.Add(TargetHitResult.EndpointOnly(start, end));

            return filtered;
        }

        /// <summary>把命中结果打包成目标数据句柄（每个命中一条 SingleTargetHit）。</summary>
        protected virtual TargetDataHandle MakeTargetData(List<TargetHitResult> hits)
        {
            var handle = new TargetDataHandle();
            foreach (var h in hits) handle.Add(new TargetData_SingleTargetHit(h));
            return handle;
        }

        /// <summary>实际 trace，由子类实现（射线 / 球扫）。（纯虚）。</summary>
        protected abstract void DoTrace(List<TargetHitResult> outHits, Vector3 start, Vector3 end);
    }
}
