// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 射线采集器（单线命中）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>射线采集：从起点沿方向打一条线到 MaxRange，收集命中。</summary>
    public class TargetActor_LineTrace : TargetActor
    {
        protected override void DoTrace(List<TargetHitResult> outHits, Vector3 start, Vector3 end)
        {
            var dir = (end - start);
            float dist = dir.magnitude;
            if (dist <= Mathf.Epsilon) return;
            dir /= dist;

            // RaycastAll 沿线收集所有命中（自带穿透），由基类按距离排序/过滤/截断 MaxHitResults。
            var hits = Physics.RaycastAll(start, dir, dist, LayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
                outHits.Add(TargetHitResult.FromRaycast(hits[i], start, end));
        }
    }
}
