// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 输入配置。
// 注：Unity 端把"绑定"交给输入后端适配器，
// 本资产只保留与后端无关的"输入缓冲窗口定义"，让核心分发不依赖具体输入方案。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>输入配置：缓冲窗口定义。的后端无关部分。</summary>
    [CreateAssetMenu(fileName = "InputConfig_New", menuName = "Likeon/GAS/Input Config")]
    public class InputConfig : ScriptableObject
    {
        [Tooltip("输入缓冲窗口定义（连招预输入）。AnimationEvent 调 OpenInputBufferWindow(tag) 开启对应窗口")]
        public List<InputBufferWindow> InputBufferDefinitions = new List<InputBufferWindow>();

        /// <summary>按 tag 找缓冲窗口定义。</summary>
        public InputBufferWindow FindBufferWindow(GameplayTag tag)
        {
            foreach (var w in InputBufferDefinitions)
                if (w != null && w.Tag.MatchesTagExact(tag)) return w;
            return null;
        }
    }
}
