// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入分发中枢。
// 与后端解耦：Unity 版由外部后端（见 UnityInput 适配器）
// 调用 ReceiveInput(tag, triggerEvent, data) 注入输入。核心的门控/分发/缓冲逻辑后端无关。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Input System Component")]
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

        // ===================== 输入动作自动绑定（对齐 UE BindInputActions）=====================
        // 从 InputConfig.InputActionMappings 订阅 Unity Input System 动作 → ReceiveInput。
        private struct BoundAction
        {
            public InputAction Action;
            public Action<InputAction.CallbackContext> Started, Performed, Canceled;
        }
        private readonly List<BoundAction> _boundActions = new List<BoundAction>();
        private readonly Dictionary<GameplayTag, InputAction> _tagToAction = new Dictionary<GameplayTag, InputAction>();

        protected virtual void OnEnable() => BindInputActions();
        protected virtual void OnDisable() => UnbindInputActions();

        /// <summary>从 InputConfig.InputActionMappings 绑定 Unity 输入动作 → ReceiveInput（对齐 UE BindInputActions）。</summary>
        public void BindInputActions()
        {
            if (inputConfig == null) return;
            foreach (var m in inputConfig.InputActionMappings)
            {
                if (m.Action == null || m.Action.action == null || !m.InputTag.IsValid) continue;
                var action = m.Action.action;
                var tag = m.InputTag;
                Action<InputAction.CallbackContext> started = ctx => ReceiveInput(tag, InputTriggerEvent.Started, ReadData(ctx));
                Action<InputAction.CallbackContext> performed = ctx => ReceiveInput(tag, InputTriggerEvent.Triggered, ReadData(ctx));
                Action<InputAction.CallbackContext> canceled = ctx => ReceiveInput(tag, InputTriggerEvent.Canceled, ReadData(ctx));
                action.started += started;
                action.performed += performed;
                action.canceled += canceled;
                action.Enable();
                _boundActions.Add(new BoundAction { Action = action, Started = started, Performed = performed, Canceled = canceled });
                _tagToAction[tag] = action;
            }
        }

        /// <summary>解绑所有已绑定的输入动作。</summary>
        public void UnbindInputActions()
        {
            foreach (var b in _boundActions)
            {
                b.Action.started -= b.Started;
                b.Action.performed -= b.Performed;
                b.Action.canceled -= b.Canceled;
            }
            _boundActions.Clear();
            _tagToAction.Clear();
        }

        /// <summary>取 InputTag 绑定的 Unity 输入动作（对齐 UE GetInputActionOfInputTag）。</summary>
        public InputAction GetInputActionOfInputTag(GameplayTag inputTag)
            => _tagToAction.TryGetValue(inputTag, out var a) ? a : null;

        /// <summary>取 InputTag 当前输入值（对齐 UE GetInputActionValueOfInputTag）。</summary>
        public InputActionData GetInputActionValueOfInputTag(GameplayTag inputTag)
            => _tagToAction.TryGetValue(inputTag, out var a) ? ReadValue(a) : InputActionData.Empty;

        // 把 Unity 输入回调读成 InputActionData（兼容 float / Vector2 / button）。
        // ⚠️ 判型必须用 ctx.valueType（本次触发绑定的"动作值类型"）：复合绑定（如 WASD 2DVector）的
        // ctx.control 是实际按下的那个键（float），按它判型会把 Vector2 轴错读成标量、方向信息全丢。
        internal static InputActionData ReadData(InputAction.CallbackContext ctx)
        {
            Vector2 value;
            try
            {
                if (ctx.valueType == typeof(Vector2)) value = ctx.ReadValue<Vector2>();
                else value = new Vector2(ctx.ReadValue<float>(), 0f);
            }
            catch { value = new Vector2(ctx.performed ? 1f : 0f, 0f); }
            return new InputActionData(value, (float)ctx.duration);
        }

        // 直接读一个动作的当前值
        private static InputActionData ReadValue(InputAction action)
        {
            try { return new InputActionData(action.ReadValue<Vector2>(), 0f); }
            catch
            {
                try { return new InputActionData(new Vector2(action.ReadValue<float>(), 0f), 0f); }
                catch { return InputActionData.Empty; }
            }
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
                FireBufferedInput(); // 其收尾可能已关掉本窗口并派发过一次关闭事件

            // 重入安全（C6）：仅当窗口仍存在时才移除 + 派发关闭事件——上面的 FireBufferedInput
            // 收尾会关掉进入时的窗口（含本窗口），无条件再 Remove + 派发会重复发一次 (tag,false)。
            if (_activeBufferWindows.Remove(bufferWindowName))
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
            // 重入安全（C6）：ProcessInput 分发过程中，处理器可能 OpenInputBufferWindow 开下一段连招窗口。
            // 收尾若无脑 CloseActiveInputBufferWindows() 关掉所有活跃窗口，会把刚开的下一段窗口一起关掉
            // → 连招预输入链断。故进入时先快照当前窗口，只关"快照里、且仍存在"的旧窗口，分发中新开的保留。
            // 用局部列表（非共享字段）以对重入安全（FireBufferedInput 自身可能被重入）。
            var windowsAtEntry = new List<GameplayTag>(_activeBufferWindows.Keys);

            ProcessInput(_currentBufferedInput.ActionData, _currentBufferedInput.InputTag, _currentBufferedInput.TriggerEvent);
            OnFireBufferedInput?.Invoke(_currentBufferedInput.ActionData, _currentBufferedInput.InputTag, _currentBufferedInput.TriggerEvent);
            _currentBufferedInput = BufferedInput.None;

            for (int i = 0; i < windowsAtEntry.Count; i++)
            {
                var k = windowsAtEntry[i];
                if (_activeBufferWindows.Remove(k)) // 仅当仍存在（可能已被重入路径关掉）
                    OnInputBufferWindowStateChanged?.Invoke(k, false);
            }
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
