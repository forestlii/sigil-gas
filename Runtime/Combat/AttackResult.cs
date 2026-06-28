// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一次攻击命中后的结算结果（可被受击方消费）。
// Unity 版去掉网络，保留数据语义。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>一次攻击结果。</summary>
    public class AttackResult
    {
        /// <summary>命中结果标签（如 Hit / Blocked / Parried）。</summary>
        public GameplayTag ImpactResult;

        /// <summary>携带的属性值（伤害量、架势伤害等）。</summary>
        public readonly List<TaggedValue> TaggedValues = new List<TaggedValue>();

        /// <summary>相关效果上下文（来源 ASC、命中点等）。</summary>
        public GameplayEffectContext EffectContext;

        /// <summary>聚合的来源/目标标签。</summary>
        public readonly GameplayTagContainer AggregatedSourceTags = new GameplayTagContainer();
        public readonly GameplayTagContainer AggregatedTargetTags = new GameplayTagContainer();

        /// <summary>命中世界坐标。</summary>
        public Vector3 HitLocation;

        /// <summary>是否已被受击方消费。</summary>
        public bool Consumed;

        public float GetTaggedValue(GameplayTag attribute, float defaultValue = 0f)
        {
            foreach (var tv in TaggedValues)
                if (tv.Attribute.MatchesTagExact(attribute)) return tv.Value;
            return defaultValue;
        }
    }
}
