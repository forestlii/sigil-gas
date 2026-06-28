// Copyright 2026 Likeon All Rights Reserved.
// Unity Input System 适配器：把 Unity 的 InputAction 回调翻译成 InputTag + InputTriggerEvent，
// 注入 InputSystemComponent。
// 仅在工程安装了 com.unity.inputsystem（定义 ENABLE_INPUT_SYSTEM）时编译。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Likeon.GAS
{
    /// <summary>
    /// 把 Unity InputActionReference 绑定到 GameplayTag（InputTag），驱动 InputSystemComponent。
    /// </summary>
    [AddComponentMenu("Likeon/GAS/Unity Input Binder")]
    [RequireComponent(typeof(InputSystemComponent))]
    public sealed class UnityInputBinder : MonoBehaviour
    {
        [Serializable]
        public struct Binding
        {
            [Tooltip("逻辑输入标签，如 InputTag.Crouch")]
            public GameplayTag InputTag;
            [Tooltip("对应的 Unity Input Action")]
            public InputActionReference Action;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();

        private InputSystemComponent _ic;
        // 记录每个 action 对应的回调，便于解绑
        private readonly List<(InputAction action, Action<InputAction.CallbackContext> started,
            Action<InputAction.CallbackContext> performed, Action<InputAction.CallbackContext> canceled)> _hooks
            = new List<(InputAction, Action<InputAction.CallbackContext>, Action<InputAction.CallbackContext>, Action<InputAction.CallbackContext>)>();

        private void Awake() => _ic = GetComponent<InputSystemComponent>();

        private void OnEnable()
        {
            foreach (var b in bindings)
            {
                if (b.Action == null || b.Action.action == null || !b.InputTag.IsValid) continue;
                var action = b.Action.action;
                var tag = b.InputTag;

                Action<InputAction.CallbackContext> started = ctx => Dispatch(tag, InputTriggerEvent.Started, ctx);
                Action<InputAction.CallbackContext> performed = ctx => Dispatch(tag, InputTriggerEvent.Triggered, ctx);
                Action<InputAction.CallbackContext> canceled = ctx => Dispatch(tag, InputTriggerEvent.Canceled, ctx);

                action.started += started;
                action.performed += performed;
                action.canceled += canceled;
                action.Enable();

                _hooks.Add((action, started, performed, canceled));
            }
        }

        private void OnDisable()
        {
            foreach (var h in _hooks)
            {
                h.action.started -= h.started;
                h.action.performed -= h.performed;
                h.action.canceled -= h.canceled;
            }
            _hooks.Clear();
        }

        private void Dispatch(GameplayTag tag, InputTriggerEvent ev, InputAction.CallbackContext ctx)
        {
            _ic.ReceiveInput(tag, ev, ReadData(ctx));
        }

        // 把 Unity 输入值读成 InputActionData（兼容 float / Vector2 / button）
        private static InputActionData ReadData(InputAction.CallbackContext ctx)
        {
            Vector2 value;
            try
            {
                var ec = ctx.control?.valueType;
                if (ec == typeof(Vector2)) value = ctx.ReadValue<Vector2>();
                else value = new Vector2(ctx.ReadValue<float>(), 0f);
            }
            catch
            {
                // button 等无值类型，按是否触发给 1/0
                value = new Vector2(ctx.performed ? 1f : 0f, 0f);
            }
            return new InputActionData(value, (float)ctx.duration);
        }
    }
}
