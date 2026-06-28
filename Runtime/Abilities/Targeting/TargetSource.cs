// Copyright 2026 Likeon All Rights Reserved.
// 目标来源：非 Actor 的目标产生器。给定施法 ASC 与事件数据，产出命中结果 + actor 列表。
// 用于效果容器（按"取自己 / 取事件里的目标"决定施加对象），不必起一个 TargetActor。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>目标来源基类（ScriptableObject）。</summary>
    public abstract class TargetSource : ScriptableObject
    {
        public abstract void GetTargets(AbilitySystemComponent targetingASC, GameplayEventData eventData,
            List<TargetHitResult> outHitResults, List<GameObject> outActors);
    }

    /// <summary>取施法者自身为目标。</summary>
    [CreateAssetMenu(menuName = "Likeon/GAS/Target Source/Self", fileName = "TS_Self")]
    public class TargetSource_Self : TargetSource
    {
        public override void GetTargets(AbilitySystemComponent targetingASC, GameplayEventData eventData,
            List<TargetHitResult> outHitResults, List<GameObject> outActors)
        {
            if (targetingASC != null) outActors.Add(targetingASC.gameObject);
        }
    }

    /// <summary>取事件数据里的目标：优先用命中结果，否则退回 EventData.Target。</summary>
    [CreateAssetMenu(menuName = "Likeon/GAS/Target Source/Event Data", fileName = "TS_EventData")]
    public class TargetSource_EventData : TargetSource
    {
        public override void GetTargets(AbilitySystemComponent targetingASC, GameplayEventData eventData,
            List<TargetHitResult> outHitResults, List<GameObject> outActors)
        {
            if (eventData == null) return;

            // 1) 事件携带的目标数据里的命中结果
            if (eventData.TargetData != null && !eventData.TargetData.IsEmpty)
            {
                var hit = eventData.TargetData.GetHitResult(0);
                if (hit.IsValidBlockingHit) { outHitResults.Add(hit); return; }
            }

            // 2) 上下文里的命中点（无完整命中结果时退而求其次）
            if (eventData.Context != null && eventData.Context.HasHitLocation && eventData.Target != null)
            {
                outHitResults.Add(new TargetHitResult
                {
                    HasHit = true, HitActor = eventData.Target,
                    Point = eventData.Context.HitLocation, Normal = Vector3.up
                });
                return;
            }

            // 3) 直接用事件目标
            if (eventData.Target != null) outActors.Add(eventData.Target);
        }
    }
}
