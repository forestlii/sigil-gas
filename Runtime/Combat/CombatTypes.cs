// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 战斗流程的基础结构。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>标签 + 数值对。（攻击结果里携带的属性值）。</summary>
    [Serializable]
    public struct TaggedValue
    {
        public GameplayTag Attribute;
        public float Value;

        public TaggedValue(GameplayTag attribute, float value) { Attribute = attribute; Value = value; }
    }

    /// <summary>
    /// 一个能力动作：要播的动画 + 播放参数 + 消耗。
    /// Unity 用 AnimationClip（→Animator State / Playable）。
    /// </summary>
    [Serializable]
    public struct AbilityAction
    {
        [Header("动画")]
        public AnimationClip Animation;
        public float PlayRate;
        [Tooltip("Animator 里对应的 State/Trigger 名（用 Animator 驱动时）")]
        public string StateName;
        [Tooltip("RootMotion 平移缩放（位移类技能放大/缩小位移）")]
        public float AnimRootMotionTranslationScale;
        public float StartTimeSeconds;

        [Header("消耗")]
        public GameplayEffect CostGameplayEffect;

        public static AbilityAction Default => new AbilityAction { PlayRate = 1f, AnimRootMotionTranslationScale = 1f };
    }

    /// <summary>带 Source/Target 标签查询的动作组。</summary>
    [Serializable]
    public struct AbilityActionsWithQuery
    {
        [Tooltip("对施法者标签的查询")]
        public GameplayTagQuery SourceTagQuery;
        [Tooltip("对目标标签的查询")]
        public GameplayTagQuery TargetTagQuery;
        public List<AbilityAction> Actions;
    }

    /// <summary>
    /// 能力动作集：按施法者/目标状态选不同动作。（含 Layered）。
    /// 这是战斗里"同一技能在不同状态下播不同动画"的数据化（与输入层多态同思路）。
    /// </summary>
    [Serializable]
    public class AbilityActionSet
    {
        public GameplayTag AbilityTag;
        [Tooltip("基础动作（默认）")]
        public List<AbilityAction> Actions = new List<AbilityAction>();
        [Tooltip("Layered：按 Source/Target 标签查询条件选择的动作组")]
        public List<AbilityActionsWithQuery> Layered = new List<AbilityActionsWithQuery>();

        /// <summary>
        /// 按施法者/目标当前标签选出动作组。优先返回首个满足查询的 Layered，否则返回基础 Actions。
        /// </summary>
        public List<AbilityAction> SelectActions(GameplayTagContainer sourceTags, GameplayTagContainer targetTags)
        {
            foreach (var layer in Layered)
            {
                bool sourceOk = layer.SourceTagQuery == null || layer.SourceTagQuery.Matches(sourceTags ?? new GameplayTagContainer());
                bool targetOk = layer.TargetTagQuery == null || layer.TargetTagQuery.Matches(targetTags ?? new GameplayTagContainer());
                if (sourceOk && targetOk && layer.Actions != null && layer.Actions.Count > 0)
                    return layer.Actions;
            }
            return Actions;
        }
    }

    /// <summary>
    /// 碰撞判定定义：用哪些骨骼 socket、按哪个 tag、目标过滤。
    /// Unity 用挂在骨骼上的 Transform（socket 等价物）。
    /// </summary>
    [Serializable]
    public struct CollisionTraceDefinition
    {
        public GameplayTag TraceTag;
        [Tooltip("武器/骨骼上的判定点 Transform（顺序代表刀刃从根到尖）")]
        public List<Transform> SocketTransforms;
        [Tooltip("判定球半径")]
        public float TraceRadius;
        [Tooltip("可命中的层")]
        public LayerMask HitLayers;
    }
}
