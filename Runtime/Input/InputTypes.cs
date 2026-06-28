// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入系统的基础结构与枚举。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>输入缓冲行为类型。</summary>
    public enum EInputBufferType
    {
        /// <summary>窗口关闭时执行最后一次按下的输入。</summary>
        LastInput,
        /// <summary>缓冲窗口期间立即执行。</summary>
        Instant,
        /// <summary>优先级高（列表靠前）的优先执行。</summary>
        HighestPriority
    }

    /// <summary>单个输入事件下处理器的执行方式。</summary>
    public enum EInputProcessorExecutionType
    {
        /// <summary>所有命中的处理器按顺序全执行。</summary>
        MatchAll,
        /// <summary>只执行第一个 CanHandleInput 通过的处理器（状态驱动多态用）。</summary>
        FirstOnly
    }

    /// <summary>允许的输入：InputTag + 允许的触发事件。</summary>
    [Serializable]
    public struct AllowedInput
    {
        public GameplayTag InputTag;
        [Tooltip("允许的触发事件（空=全部允许）")]
        public List<InputTriggerEvent> TriggerEvents;

        public bool Matches(GameplayTag tag, InputTriggerEvent ev)
            => InputTag.MatchesTagExact(tag) && (TriggerEvents == null || TriggerEvents.Count == 0 || TriggerEvents.Contains(ev));
    }

    /// <summary>缓冲的输入记录。</summary>
    [Serializable]
    public struct BufferedInput
    {
        public GameplayTag InputTag;
        public InputActionData ActionData;
        public InputTriggerEvent TriggerEvent;

        public BufferedInput(GameplayTag tag, InputActionData data, InputTriggerEvent ev)
        {
            InputTag = tag; ActionData = data; TriggerEvent = ev;
        }

        public bool IsValid => InputTag.IsValid;
        public static readonly BufferedInput None = default;
    }

    /// <summary>输入缓冲窗口定义。</summary>
    [Serializable]
    public class InputBufferWindow
    {
        public GameplayTag Tag;
        public EInputBufferType BufferType = EInputBufferType.LastInput;
        [Tooltip("本窗口接受缓冲的输入列表")]
        public List<AllowedInput> AllowedInputs = new List<AllowedInput>();

        /// <summary>找到某输入在 AllowedInputs 里的下标（即优先级，越小越高）；找不到返回 -1。</summary>
        public int IndexOfAllowedInput(GameplayTag inputTag, InputTriggerEvent ev)
        {
            for (int i = 0; i < AllowedInputs.Count; i++)
                if (AllowedInputs[i].Matches(inputTag, ev)) return i;
            return -1;
        }
    }

    /// <summary>状态→输入权限关系：满足查询时放行其 AllowedInputs。</summary>
    [Serializable]
    public struct InputTagRelationship
    {
        [Tooltip("对角色当前标签的查询；满足时本条关系生效")]
        public GameplayTagQuery ActorTagQuery;
        public List<AllowedInput> AllowedInputs;

        public int IndexOfAllowedInput(GameplayTag inputTag, InputTriggerEvent ev)
        {
            if (AllowedInputs == null) return -1;
            for (int i = 0; i < AllowedInputs.Count; i++)
                if (AllowedInputs[i].Matches(inputTag, ev)) return i;
            return -1;
        }
    }
}
