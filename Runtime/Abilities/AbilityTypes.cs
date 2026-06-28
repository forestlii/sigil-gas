// Copyright 2026 Likeon All Rights Reserved.
// 能力相关的基础类型：激活策略枚举、标签计数、效果容器。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 技能激活策略——管理技能间的并行与独占打断。
    /// 例"冲刺中按键触发滑铲"会打断其它独占技能，就靠它。
    /// </summary>
    public enum EAbilityActivationPolicy
    {
        /// <summary>并行运行，不与其它技能互斥。</summary>
        Parallel,
        /// <summary>独占，但可被其它独占技能取消并替换。</summary>
        Replaceable,
        /// <summary>独占，且阻止其它独占技能激活。</summary>
        Blocking,
        MAX
    }

    /// <summary>标签 + 计数。用于激活期间挂载的松散标签。</summary>
    [Serializable]
    public struct GameplayTagCount
    {
        public GameplayTag Tag;
        [Min(1)] public int Count;

        public GameplayTagCount(GameplayTag tag, int count = 1) { Tag = tag; Count = Mathf.Max(1, count); }
    }

    /// <summary>
    /// 效果容器：一组"命中目标后要施加的 GameplayEffect"。技能按标签批量施效。
    /// </summary>
    [Serializable]
    public struct GameplayEffectContainer
    {
        [Tooltip("命中目标时施加给目标的效果")]
        public List<GameplayEffect> TargetGameplayEffects;
        [Tooltip("施加给自己的效果")]
        public List<GameplayEffect> SelfGameplayEffects;
    }

    /// <summary>效果容器的一次实例化（带目标与 spec）。</summary>
    public class GameplayEffectContainerSpec
    {
        public readonly List<GameplayEffectSpec> TargetEffectSpecs = new List<GameplayEffectSpec>();
        public readonly List<GameplayEffectSpec> SelfEffectSpecs = new List<GameplayEffectSpec>();
        public readonly List<AbilitySystemComponent> Targets = new List<AbilitySystemComponent>();
        public bool HasValidTargets => Targets.Count > 0;
    }
}
