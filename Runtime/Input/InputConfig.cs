// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入配置：InputTag↔InputAction 绑定映射 + 输入缓冲窗口定义。
// 策划在此资产集中配按键映射与连招缓冲，可跨角色复用。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Likeon.GAS
{
    /// <summary>一条输入映射：把逻辑 InputTag 绑到一个 Unity 输入动作。</summary>
    [System.Serializable]
    public struct InputActionMapping
    {
        [Tooltip("逻辑输入标签，如 InputTag.Melee")]
        public GameplayTag InputTag;
        [Tooltip("对应的 Unity Input Action")]
        public InputActionReference Action;
        [Tooltip("是否持续读值（移动/瞄准等轴输入设 true；按钮可设 false）")]
        public bool ValueBinding;
    }

    /// <summary>
    /// 输入配置资产。两部分：
    /// ① InputActionMappings —— InputTag↔InputAction 绑定（InputSystemComponent 启用时据此自动订阅 → ReceiveInput）；
    /// ② InputBufferDefinitions —— 输入缓冲窗口（连招预输入）。
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig_New", menuName = "Sigil/GAS/Input Config")]
    public class InputConfig : ScriptableObject
    {
        [Header("输入动作映射 InputTag ↔ InputAction")]
        [Tooltip("策划配：每个逻辑 InputTag 绑一个 Unity Input Action。InputSystemComponent 启用时自动订阅 → ReceiveInput。")]
        public List<InputActionMapping> InputActionMappings = new List<InputActionMapping>();

        [Header("输入缓冲窗口 Input Buffer Windows")]
        [Tooltip("连招预输入。AnimationEvent 调 OpenInputBufferWindow(tag) 开启对应窗口")]
        public List<InputBufferWindow> InputBufferDefinitions = new List<InputBufferWindow>();

        /// <summary>按 tag 找缓冲窗口定义。</summary>
        public InputBufferWindow FindBufferWindow(GameplayTag tag)
        {
            foreach (var w in InputBufferDefinitions)
                if (w != null && w.Tag.MatchesTagExact(tag)) return w;
            return null;
        }

        /// <summary>按 InputTag 取绑定的 InputActionReference（找不到返回 null）。</summary>
        public InputActionReference GetActionRef(GameplayTag tag)
        {
            foreach (var m in InputActionMappings)
                if (m.InputTag.MatchesTagExact(tag)) return m.Action;
            return null;
        }
    }
}
