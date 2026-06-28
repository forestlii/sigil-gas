// Copyright 2026 Likeon All Rights Reserved.
// 相机混合栈：把若干相机行为叠在一起，按各自混入权重融合成一个最终视图。
// index 0 = 最新压入（最上层）。最上层完全生效后，下方被遮住的层会被丢弃。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>相机行为的混合栈。</summary>
    public sealed class CameraBlendStack
    {
        private readonly List<CameraBehavior> _layers = new List<CameraBehavior>();

        public int Count => _layers.Count;
        public CameraBehavior Top => _layers.Count > 0 ? _layers[0] : null;

        /// <summary>压入一个相机行为到最上层（开始混入）。</summary>
        public void Push(CameraBehavior behavior)
        {
            if (behavior == null) return;
            if (_layers.Count > 0 && _layers[0] == behavior) return; // 已在最上层

            // 若该行为已在栈中，先取出并记下它当前权重，压回顶部时承接（避免跳变）
            float carriedWeight = 0f;
            int existingIndex = _layers.IndexOf(behavior);
            if (existingIndex >= 0)
            {
                carriedWeight = _layers[existingIndex].Weight;
                _layers.RemoveAt(existingIndex);
            }

            _layers.Insert(0, behavior);
            behavior.OnEnter();
            if (carriedWeight > 0f) behavior.SetWeight(carriedWeight);
        }

        /// <summary>移除一个相机行为。</summary>
        public void Pop(CameraBehavior behavior)
        {
            if (behavior != null && _layers.Remove(behavior)) behavior.OnExit();
        }

        /// <summary>推进并融合整个栈，得到最终视图。</summary>
        public CameraView Evaluate(Transform target, float deltaTime)
        {
            int n = _layers.Count;
            if (n == 0) return CameraView.Default;

            // 先各自推进
            for (int i = 0; i < n; i++) _layers[i].Tick(target, deltaTime);

            // 从最底层起，逐层向上按权重叠加（上层覆盖下层）
            CameraView resolved = _layers[n - 1].View;
            for (int i = n - 2; i >= 0; i--)
                resolved = CameraView.Mix(resolved, _layers[i].View, _layers[i].Weight);

            // 最上层已完全覆盖 → 丢弃其下被遮住的层
            if (n > 1 && _layers[0].Weight >= 1f)
            {
                for (int i = n - 1; i >= 1; i--) _layers[i].OnExit();
                _layers.RemoveRange(1, n - 1);
            }

            return resolved;
        }

        /// <summary>最上层的权重与类型标签。</summary>
        public void GetTopBlend(out float topWeight, out GameplayTag topTag)
        {
            if (_layers.Count > 0) { topWeight = _layers[0].Weight; topTag = _layers[0].CameraTag; }
            else { topWeight = 0f; topTag = GameplayTag.None; }
        }
    }
}
