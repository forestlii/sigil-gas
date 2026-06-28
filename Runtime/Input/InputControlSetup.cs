// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一套"检查器 + 处理器 + 执行模式"，可压栈切换。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 输入控制集（ScriptableObject 资产）。持有门控检查器与分发处理器。
    /// 可被 InputSystemComponent 压栈/弹栈，按上下文（战斗/载具/UI）整套切换。
    /// </summary>
    [CreateAssetMenu(fileName = "InputControlSetup_New", menuName = "Likeon/GAS/Input Control Setup")]
    public class InputControlSetup : ScriptableObject
    {
        [Header("门控检查器（全部通过才放行）")]
        [SerializeReference] private List<InputChecker> inputCheckers = new List<InputChecker>();

        [Tooltip("放行失败时，是否尝试存入输入缓冲（连招）。UI 类设置通常关闭")]
        [SerializeField] private bool enableInputBuffer = false;

        [Header("分发处理器")]
        [Tooltip("FirstOnly=只执行第一个通过的（状态驱动多态）；MatchAll=全部命中的都执行")]
        [SerializeField] private EInputProcessorExecutionType inputProcessorExecutionType = EInputProcessorExecutionType.MatchAll;
        [SerializeReference] private List<InputProcessor> inputProcessors = new List<InputProcessor>();

        // ---- 运行时/代码驱动配置 API（也便于测试） ----
        /// <summary>执行模式（FirstOnly / MatchAll）。</summary>
        public EInputProcessorExecutionType ExecutionType
        {
            get => inputProcessorExecutionType;
            set => inputProcessorExecutionType = value;
        }

        /// <summary>是否启用输入缓冲。</summary>
        public bool EnableInputBuffer
        {
            get => enableInputBuffer;
            set => enableInputBuffer = value;
        }

        /// <summary>追加一个门控检查器。</summary>
        public void AddChecker(InputChecker checker)
        {
            if (checker != null) inputCheckers.Add(checker);
        }

        /// <summary>追加一个处理器（顺序即优先级；FirstOnly 下靠前的先命中）。</summary>
        public void AddProcessor(InputProcessor processor)
        {
            if (processor != null) inputProcessors.Add(processor);
        }

        /// <summary>门控 + 缓冲。通过返回 true。</summary>
        public bool CheckInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            if (InternalCheckInput(ic, data, inputTag, triggerEvent))
            {
                ic.RegisterPassedInputEntry(new BufferedInput(inputTag, data, triggerEvent));
                return true;
            }

            if (enableInputBuffer && ic.TrySaveInput(data, inputTag, triggerEvent))
            {
                ic.RegisterBufferedInputEntry(new BufferedInput(inputTag, data, triggerEvent));
                return false;
            }

            ic.RegisterBlockedInputEntry(new BufferedInput(inputTag, data, triggerEvent));
            return false;
        }

        // 所有检查器都通过才算通过（无检查器=放行）。
        private bool InternalCheckInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            if (inputCheckers.Count == 0) return true;
            foreach (var checker in inputCheckers)
            {
                if (checker == null) continue;
                if (!checker.CheckInput(ic, data, inputTag, triggerEvent)) return false;
            }
            return true;
        }

        /// <summary>多态分发。</summary>
        public void HandleInput(InputSystemComponent ic, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            var processors = FilterInputProcessors(inputTag, triggerEvent);
            foreach (var processor in processors)
            {
                if (processor.CanHandleInput(ic, data, inputTag, triggerEvent))
                {
                    processor.HandleInput(ic, data, inputTag, triggerEvent);
                    if (inputProcessorExecutionType == EInputProcessorExecutionType.FirstOnly)
                        return; // ★ 第一个通过的执行后即返回——状态驱动多态的精确落点
                }
            }
        }

        // 筛出监听该 InputTag + TriggerEvent 的处理器。
        private readonly List<InputProcessor> _filterScratch = new List<InputProcessor>();
        private List<InputProcessor> FilterInputProcessors(GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            _filterScratch.Clear();
            foreach (var p in inputProcessors)
            {
                if (p != null && !p.InputTags.IsEmpty && p.InputTags.HasTagExact(inputTag) && p.TriggerEvents.Contains(triggerEvent))
                    _filterScratch.Add(p);
            }
            return _filterScratch;
        }
    }
}
