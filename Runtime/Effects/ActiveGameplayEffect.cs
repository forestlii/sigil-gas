// Copyright 2026 Likeon All Rights Reserved.
// 已施加且仍存活的 Duration/Infinite 效果。

namespace Likeon.GAS
{
    /// <summary>已激活效果的句柄。，用于后续移除/查询。</summary>
    public readonly struct ActiveGameplayEffectHandle
    {
        public readonly int Id;
        public ActiveGameplayEffectHandle(int id) { Id = id; }
        public bool IsValid => Id > 0;
        public static readonly ActiveGameplayEffectHandle Invalid = new ActiveGameplayEffectHandle(0);
        public override int GetHashCode() => Id;
        public override bool Equals(object obj) => obj is ActiveGameplayEffectHandle h && h.Id == Id;
    }

    /// <summary>
    /// 一个存活中的 Duration/Infinite 效果实例。ASC 每帧 Tick 它：
    /// 推进时长、按周期结算、到期移除。
    /// </summary>
    public sealed class ActiveGameplayEffect
    {
        public ActiveGameplayEffectHandle Handle { get; }
        public GameplayEffectSpec Spec { get; }

        /// <summary>剩余时长（HasDuration 用；Infinite 为正无穷）。</summary>
        public float TimeRemaining;
        /// <summary>距下次周期结算的剩余时间。</summary>
        public float PeriodRemaining;
        /// <summary>本效果是否因 Ongoing 标签条件不满足而被抑制（CurrentValue 不参与聚合）。</summary>
        public bool Inhibited;

        public GameplayEffect Def => Spec.Def;

        public ActiveGameplayEffect(ActiveGameplayEffectHandle handle, GameplayEffectSpec spec)
        {
            Handle = handle;
            Spec = spec;
            TimeRemaining = spec.Def.DurationType == EGameplayEffectDurationType.HasDuration
                ? spec.Def.Duration
                : float.PositiveInfinity;
            PeriodRemaining = spec.Def.IsPeriodic ? spec.Def.Period : 0f;
        }

        public bool IsExpired => Def.DurationType == EGameplayEffectDurationType.HasDuration && TimeRemaining <= 0f;
    }
}
