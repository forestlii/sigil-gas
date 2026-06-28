// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// GAS 的"表现钩子"，用 tag 把 VFX/SFX 与逻辑解耦。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>Cue 事件类型。</summary>
    public enum EGameplayCueEvent
    {
        /// <summary>持续型 cue 开始（Duration/Infinite 效果施加时）。</summary>
        OnActive,
        /// <summary>持续期间（每帧/周期）。</summary>
        WhileActive,
        /// <summary>瞬时执行（Instant 效果 / 一次性命中表现）。</summary>
        Executed,
        /// <summary>持续型 cue 结束（效果移除时）。</summary>
        Removed
    }

    /// <summary>Cue 携带的表现参数。</summary>
    public class GameplayCueParameters
    {
        public GameObject Instigator;
        public GameObject SourceObject;
        public Vector3 Location;
        public Vector3 Normal = Vector3.up;
        public float Magnitude;
        public GameplayEffectContext EffectContext;
        public GameplayTagContainer AggregatedSourceTags;

        public GameplayCueParameters() { }

        public static GameplayCueParameters At(Vector3 location, Vector3 normal = default, GameObject instigator = null)
            => new GameplayCueParameters { Location = location, Normal = normal == default ? Vector3.up : normal, Instigator = instigator };
    }
}
