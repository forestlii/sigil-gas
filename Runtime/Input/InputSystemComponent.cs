// Copyright 2026 Likeon All Rights Reserved.
// 输入分发中枢。
// 与后端解耦：Unity 版由外部后端（见 UnityInput 适配器）
// 调用 ReceiveInput(tag, triggerEvent, data) 注入输入。核心的门控/分发/缓冲逻辑后端无关。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Input System Component")]
    public class InputSystemComponent : MonoBehaviour
    {
        [Header("依赖")]
        [Tooltip("驱动技能的 ASC（留空则在同物体上自动查找）")]
        [SerializeField] private AbilitySystemComponent abilitySystem;

        [Tooltip("输入配置（缓冲窗口定义）")]
        [SerializeField] private InputConfig inputConfig;

        [Tooltip("若开启，输入只广播 OnReceivedInput，不走内部门控/分发（外部自行处理）")]
        [SerializeField] private bool processingInputExternally = false;

        [Tooltip("保留的输入记录条数上限")]
        [SerializeField, Range(0, 10)] private int maxInputEntriesNum = 5;

        [Header("输入控制集栈（最后一个为当前）")]
        [SerializeField] private List<InputControlSetup> inputControlSetups = new List<InputControlSetup>();

        public AbilitySystemComponent AbilitySystem => abilitySystem;
        public InputConfig Config => inputConfig;

        // 事件
        public event Action<InputActionData, GameplayTag, InputTriggerEvent> OnReceivedInput;
        public event Action<InputActionData, GameplayTag, InputTriggerEvent> OnFireBufferedInput;
        public event Action<GameplayTag, bool> OnInputBufferWindowStateChanged;

        // 运行时状态
        private readonly Dictionary<GameplayTag, InputActionData> _lastInputValues = new Dictionary<GameplayTag, InputActionData>();
        private readonly List<BufferedInput> _passedEntries = new List<BufferedInput>();
        private readonly List<BufferedInput> _blockedEntries = new List<BufferedInput>();
        private readonly List<BufferedInput> _bufferedEntries = new List<BufferedInput>();
        private readonly Dictionary<GameplayTag, BufferedInput> _activeBufferWindows = new Dictionary<GameplayTag, BufferedInput>();
        private BufferedInput _currentBufferedInput;

        private readonly GameplayTagContainer _actorTagsCache = new GameplayTagContainer();

        protected virtual void Awake()
        {
            if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystemComponent>();
        }

        // ===================== 控制集栈 =====================
        public InputControlSetup GetCurrentInputSetup()
            => inputControlSetups.Count > 0 ? inputControlSetups[inputControlSetups.Count - 1] : null;

        /// <summary>压入一套控制集为当前（进瞄准/载具/UI 时用）。</summary>
        public void PushInputSetup(InputControlSetup newSetup)
        {
            if (newSetup != null) inputControlSetups.Add(newSetup);
        }

        /// <summary>弹出当前控制集。仅剩一套时不操作。</summary>
        public void PopInputSetup()
        {
            if (inputControlSetups.Count > 1) inputControlSetups.RemoveAt(inputControlSetups.Count - 1);
        }

        // ===================== 输入入口 =====================
        /// <summary>
        /// 注入一次输入（由输入后端调用）。：
        /// 非外部处理 且 CheckInputAllowed 通过 → ProcessInput。
        /// </summary>
        public void ReceiveInput(GameplayTag inputTag, InputTriggerEvent triggerEvent, InputActionData data)
        {
            _lastInputValues[inputTag] = data;
            OnReceivedInput?.Invoke(data, inputTag, triggerEvent);

            if (!processingInputExternally && CheckInputAllowed(data, inputTag, triggerEvent))
                ProcessInput(data, inputTag, triggerEvent);
        }

        /// <summary>门控检查（跑当前控制集的检查器/缓冲）。</summary>
        public bool CheckInputAllowed(InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            var setup = GetCurrentInputSetup();
            return setup != null && setup.CheckInput(this, data, inputTag, triggerEvent);
        }

        /// <summary>分发处理（跑当前控制集的处理器）。</summary>
        public void ProcessInput(InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            GetCurrentInputSetup()?.HandleInput(this, data, inputTag, triggerEvent);
        }

        /// <summary>取角色当前状态标签（供检查器/处理器查状态）。（读 ASC 拥有标签）。</summary>
        public GameplayTagContainer GetActorTags()
        {
            _actorTagsCache.Clear();
            if (abilitySystem != null) abilitySystem.GetOwnedGameplayTags(_actorTagsCache);
            return _actorTagsCache;
        }

        public bool TryGetLastInputValue(GameplayTag inputTag, out InputActionData data)
            => _lastInputValues.TryGetValue(inputTag, out data);

        // ===================== 输入记录 =====================
        public void RegisterPassedInputEntry(BufferedInput entry) => PushBounded(_passedEntries, entry);
        public void RegisterBlockedInputEntry(BufferedInput entry) => PushBounded(_blockedEntries, entry);
        public void RegisterBufferedInputEntry(BufferedInput entry) => PushBounded(_bufferedEntries, entry);

        private void PushBounded(List<BufferedInput> list, BufferedInput entry)
        {
            list.Add(entry);
            while (list.Count > maxInputEntriesNum) list.RemoveAt(0);
        }

        public IReadOnlyList<BufferedInput> PassedInputEntries => _passedEntries;
        public IReadOnlyList<BufferedInput> BlockedInputEntries => _blockedEntries;
        public IReadOnlyList<BufferedInput> BufferedInputEntries => _bufferedEntries;

        // ===================== 输入缓冲 =====================
        /// <summary>开启一个缓冲窗口（攻击动画的 Animation Event 调用）。</summary>
        public void OpenInputBufferWindow(GameplayTag bufferWindowName)
        {
            if (!bufferWindowName.IsValid) { Debug.LogWarning("[Sigil] OpenInputBufferWindow 收到无效窗口名"); return; }
            if (_activeBufferWindows.ContainsKey(bufferWindowName)) return;
            if (inputConfig == null || inputConfig.FindBufferWindow(bufferWindowName) == null) return;

            _activeBufferWindows[bufferWindowName] = BufferedInput.None;
            OnInputBufferWindowStateChanged?.Invoke(bufferWindowName, true);
        }

        /// <summary>关闭一个缓冲窗口：若窗口里存了输入则触发它。</summary>
        public void CloseInputBufferWindow(GameplayTag bufferWindowName)
        {
            if (!_activeBufferWindows.TryGetValue(bufferWindowName, out var buffered)) return;

            _currentBufferedInput = buffered;
            if (_currentBufferedInput.IsValid)
                FireBufferedInput();

            _activeBufferWindows.Remove(bufferWindowName);
            OnInputBufferWindowStateChanged?.Invoke(bufferWindowName, false);
        }

        /// <summary>关闭所有激活窗口（不触发）。</summary>
        public void CloseActiveInputBufferWindows()
        {
            if (_activeBufferWindows.Count == 0) return;
            var keys = new List<GameplayTag>(_activeBufferWindows.Keys);
            foreach (var k in keys)
            {
                _activeBufferWindows.Remove(k);
                OnInputBufferWindowStateChanged?.Invoke(k, false);
            }
        }

        // 触发当前缓冲的输入：重新走分发。
        private void FireBufferedInput()
        {
            ProcessInput(_currentBufferedInput.ActionData, _currentBufferedInput.InputTag, _currentBufferedInput.TriggerEvent);
            OnFireBufferedInput?.Invoke(_currentBufferedInput.ActionData, _currentBufferedInput.InputTag, _currentBufferedInput.TriggerEvent);
            _currentBufferedInput = BufferedInput.None;
            CloseActiveInputBufferWindows();
        }

        /// <summary>尝试把被拦输入存入任一激活窗口。</summary>
        public bool TrySaveInput(InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            if (_activeBufferWindows.Count == 0) return false;
            int counter = 0;
            var keys = new List<GameplayTag>(_activeBufferWindows.Keys);
            foreach (var windowName in keys)
                if (TrySaveAsBufferedInput(windowName, data, inputTag, triggerEvent)) counter++;
            return counter > 0;
        }

        // 按窗口的 BufferType 存输入。
        private bool TrySaveAsBufferedInput(GameplayTag windowName, InputActionData data, GameplayTag inputTag, InputTriggerEvent triggerEvent)
        {
            if (!_activeBufferWindows.TryGetValue(windowName, out var buffered)) return false;
            var def = inputConfig != null ? inputConfig.FindBufferWindow(windowName) : null;
            if (def == null) return false;

            int allowedIndex = def.IndexOfAllowedInput(inputTag, triggerEvent);
            if (allowedIndex == -1) return false;

            // Instant：立即触发
            if (def.BufferType == EInputBufferType.Instant)
            {
                buffered = new BufferedInput(inputTag, data, triggerEvent);
                _activeBufferWindows[windowName] = buffered;
                _currentBufferedInput = buffered;
                FireBufferedInput();
                _activeBufferWindows.Remove(windowName);
                return true;
            }

            // HighestPriority：仅当新输入优先级更高（下标更小）才覆盖。
            // ⚠️ 与源码出入：
            //    最后也会 fall-through 覆盖成最新（等同 LastInput，疑似源码 bug）。
            //    这里选符合枚举本意的行为：保留更高优先级的那个，不被覆盖。
            if (buffered.IsValid && def.BufferType == EInputBufferType.HighestPriority)
            {
                int existingIndex = def.IndexOfAllowedInput(buffered.InputTag, buffered.TriggerEvent);
                if (existingIndex != -1 && allowedIndex < existingIndex)
                {
                    _activeBufferWindows[windowName] = new BufferedInput(inputTag, data, triggerEvent);
                    return true;
                }
                // 否则保留原有更高优先级的输入（不覆盖）
                if (existingIndex != -1) return true;
            }

            // LastInput（默认）：覆盖为最新
            _activeBufferWindows[windowName] = new BufferedInput(inputTag, data, triggerEvent);
            return true;
        }
    }
}
