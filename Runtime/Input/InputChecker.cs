// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入门控（决定输入是否放行）。
// Unity 端用 [Serializable] 多态类 + [SerializeReference] 内联到控制集，

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>输入检查器基类。返回 false 则拦截该输入（可进缓冲）。</summary>
    [Serializable]
    public abstract class InputChecker
    {
        /// <summary>检查输入是否有效/放行。</summary>
        public abstract bool CheckInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent);
    }

    /// <summary>
    /// 基于状态标签关系的门控。按角色当前标签查询，决定某 InputTag 此刻是否放行。
    ///—— 状态驱动多态的"层 1：输入门控"。
    /// </summary>
    [Serializable]
    public sealed class InputChecker_TagRelationship : InputChecker
    {
        [Tooltip("状态→放行输入 的关系列表")]
        [SerializeField] private List<InputTagRelationship> inputTagRelationships = new List<InputTagRelationship>();

        public override bool CheckInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            var actorTags = ic.GetActorTags();

            foreach (var rel in inputTagRelationships)
            {
                // 忠实复刻源码：TagQuery 优先。查询为空或不匹配 → 跳过本条。
                if (rel.ActorTagQuery == null || rel.ActorTagQuery.IsEmpty || !rel.ActorTagQuery.Matches(actorTags))
                    continue;

                // 查询匹配：命中放行列表则放行，否则拦截（本条已是决定性的）。
                return rel.IndexOfAllowedInput(inputTag, triggerEvent) != -1;
            }

            // 没有任何关系匹配 → 默认放行。
            return true;
        }
    }
}
