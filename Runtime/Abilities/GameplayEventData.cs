// Copyright 2026 Likeon All Rights Reserved.
// 通过 GameplayEvent 触发技能/在技能间传递的数据负载。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 游戏事件数据。用 SendGameplayEvent(tag, data) 触发监听该 tag 的技能，
    /// 也用于命中判定把目标/数值传给结算。
    /// </summary>
    public class GameplayEventData
    {
        public GameplayTag EventTag;
        public GameObject Instigator;
        public GameObject Target;
        public GameplayEffectContext Context;
        public GameplayTagContainer InstigatorTags = new GameplayTagContainer();
        public GameplayTagContainer TargetTags = new GameplayTagContainer();
        public float EventMagnitude;
        public object OptionalObject;

        /// <summary>目标数据（一次采集的结果，可空）。::TargetData。</summary>
        public TargetDataHandle TargetData;

        public GameplayEventData() { }
        public GameplayEventData(GameplayTag tag) { EventTag = tag; }
    }
}
