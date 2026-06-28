// Copyright 2026 Likeon All Rights Reserved.
// 球扫采集器（带半径的扫掠，更易命中）。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>球扫采集：从起点沿方向以 Radius 半径扫到 MaxRange。</summary>
    public class TargetActor_SphereTrace : TargetActor
    {
        /// <summary>球扫半径。的半径。</summary>
        public float Radius = 0.5f;

        protected override void DoTrace(List<TargetHitResult> outHits, Vector3 start, Vector3 end)
        {
            var dir = (end - start);
            float dist = dir.magnitude;
            if (dist <= Mathf.Epsilon) return;
            dir /= dist;

            var hits = Physics.SphereCastAll(start, Mathf.Max(0.01f, Radius), dir, dist, LayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
                outHits.Add(TargetHitResult.FromRaycast(hits[i], start, end));
        }
    }
}
