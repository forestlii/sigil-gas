// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一次攻击命中后"对目标做什么"的数据。
// Unity 用 ScriptableObject 资产。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>SetByCaller 标签→数值 的一条。供 Inspector 配置。</summary>
    [Serializable]
    public struct SetByCallerMagnitude
    {
        public GameplayTag Tag;
        public float Value;
    }

    /// <summary>攻击定义资产。命中目标时按它施加效果/表现。</summary>
    [CreateAssetMenu(fileName = "Attack_New", menuName = "Sigil/Combat/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        [Header("攻击标签（近战/远程、劈砍/打击……）")]
        [Tooltip("作为动态资产标签加进效果 spec")]
        public List<GameplayTag> AttackTags = new List<GameplayTag>();

        [Header("SetByCaller（如伤害修正系数）")]
        public List<SetByCallerMagnitude> SetByCallerMagnitudes = new List<SetByCallerMagnitude>();

        [Header("施加给目标的效果")]
        [Tooltip("命中目标时施加的主效果（伤害 GE）")]
        public GameplayEffect TargetEffect;
        [Tooltip("目标效果等级（<1 时用技能等级）")]
        public int TargetEffectLevel = 1;
        [Tooltip("效果容器：命中时批量施加给目标的额外效果")]
        public GameplayEffectContainer TargetEffectContainer;

        [Header("命中表现 GameplayCue")]
        public List<GameplayTag> TargetGameplayCues = new List<GameplayTag>();

        [Header("受击反应 HitReaction")]
        [Tooltip("击退距离（米）")]
        public float KnockbackDistance = 1f;
        [Min(1f)] public float KnockbackMultiplier = 1f;

        [Header("打击感 Feedback")]
        [Tooltip("命中时动画停滞时长（<=0 关闭），即 hit-stop")]
        public float HitStallingDuration = 0f;
        [Range(0.1f, 0.9f)] public float HitPlayRateFactor = 0.1f;
    }
}
