// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入处理器（多态分发落点）。
// 一个 InputTag 下可挂多个处理器，各有 CheckCanHandleInput 状态条件，FirstOnly 模式选第一个通过的。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>输入处理器基类。</summary>
    [Serializable]
    public abstract class InputProcessor
    {
        [Tooltip("本处理器监听的 InputTag（空=不响应任何）")]
        public GameplayTagContainer InputTags = new GameplayTagContainer();

        [Tooltip("本处理器响应的触发事件")]
        public List<InputTriggerEvent> TriggerEvents = new List<InputTriggerEvent> { InputTriggerEvent.Started };

        /// <summary>是否能处理该输入（在此查角色状态 tag）。</summary>
        public bool CanHandleInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
            => CheckCanHandleInput(ic, data, inputTag, triggerEvent);

        /// <summary>处理输入：按触发事件分发到对应 Handle*。</summary>
        public void HandleInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            switch (triggerEvent)
            {
                case InputTriggerEvent.Started:   HandleInputStarted(ic, data, inputTag); break;
                case InputTriggerEvent.Triggered: HandleInputTriggered(ic, data, inputTag); break;
                case InputTriggerEvent.Ongoing:   HandleInputOngoing(ic, data, inputTag); break;
                case InputTriggerEvent.Canceled:  HandleInputCanceled(ic, data, inputTag); break;
                case InputTriggerEvent.Completed: HandleInputCompleted(ic, data, inputTag); break;
            }
        }

        protected virtual bool CheckCanHandleInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent) => true;

        protected virtual void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) { }
        protected virtual void HandleInputTriggered(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) { }
        protected virtual void HandleInputOngoing(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) { }
        protected virtual void HandleInputCanceled(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) { }
        protected virtual void HandleInputCompleted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag) { }
    }

    /// <summary>
    /// 处理器：按角色状态激活技能。这是"状态驱动按键多态"的核心落点（层 2）。
    /// 配两个该处理器监听同一 InputTag、FirstOnly：
    ///   SlideProcessor（StateQuery=含 Sprint，排前）→ 激活滑铲；
    ///   CrouchProcessor（StateQuery 空，排后）→ 激活下蹲。
    /// 冲刺时滑铲先命中，否则落到下蹲。
    /// </summary>
    [Serializable]
    public sealed class InputProcessor_ActivateAbilityByTag : InputProcessor
    {
        [Tooltip("状态条件：满足（对角色当前标签）才处理。空查询=无条件。状态驱动多态核心")]
        public GameplayTagQuery StateQuery;

        [Tooltip("要激活的技能（按其 AbilityTags 匹配）")]
        public GameplayTag AbilityTag;

        protected override bool CheckCanHandleInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            var asc = ic.AbilitySystem;
            if (asc == null) return false;
            if (StateQuery != null && !StateQuery.IsEmpty)
            {
                var tags = new GameplayTagContainer();
                asc.GetOwnedGameplayTags(tags);
                if (!StateQuery.Matches(tags)) return false;
            }
            return true;
        }

        protected override void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag)
            => ic.AbilitySystem?.TryActivateAbilitiesByTag(AbilityTag);
    }

    /// <summary>处理器：广播一个 GameplayEvent（驱动监听该 tag 的技能/逻辑）。对应常见的 SendGameplayEvent 处理器。</summary>
    [Serializable]
    public sealed class InputProcessor_SendGameplayEvent : InputProcessor
    {
        public GameplayTag EventTag;

        protected override void HandleInputStarted(InputSystemComponent ic, InputActionData data, GameplayTag inputTag)
            => ic.AbilitySystem?.SendGameplayEvent(EventTag);
    }
}
