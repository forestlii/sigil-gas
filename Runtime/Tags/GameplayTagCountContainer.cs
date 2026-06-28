// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 带引用计数的标签集合，用于 ASC 的"拥有标签"。
// 关键语义：加入子标签 "A.B.C" 时，父标签 "A.B"、"A" 计数同步 +1，
// 使得 HasMatchingTag("A") 在只加了 "A.B.C" 时也成立。

using System;
using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>
    /// 引用计数式标签容器。ASC 用它维护"角色当前拥有的状态标签"。
    /// 多个来源（多个激活中的技能 / GameplayEffect）可能加同一标签，靠计数避免一方移除误删。
    /// </summary>
    public sealed class GameplayTagCountContainer
    {
        // 显式加入的标签计数（不含自动展开的父标签）。
        private readonly Dictionary<GameplayTag, int> _explicitCounts = new Dictionary<GameplayTag, int>();
        // 含父标签展开后的总计数，用于 O(1) 的 HasMatchingTag 查询。
        private readonly Dictionary<GameplayTag, int> _tagCounts = new Dictionary<GameplayTag, int>();

        /// <summary>某标签计数从 0→正 或 正→0 时触发。 (tag, 新计数是否>0)</summary>
        public event Action<GameplayTag, bool> OnTagCountChanged;
        public event Action<GameplayTag, int> OnAnyTagCountChanged;

        /// <summary>层级查询：是否拥有匹配该标签的计数。</summary>
        public bool HasMatchingTag(GameplayTag tag)
        {
            return tag.IsValid && _tagCounts.TryGetValue(tag, out int c) && c > 0;
        }

        public int GetTagCount(GameplayTag tag) => _tagCounts.TryGetValue(tag, out int c) ? c : 0;
        public int GetExplicitTagCount(GameplayTag tag) => _explicitCounts.TryGetValue(tag, out int c) ? c : 0;

        /// <summary>增加/减少一个标签的计数（delta 可为负）。返回是否发生了"有无"状态翻转。</summary>
        public bool UpdateTagCount(GameplayTag tag, int delta)
        {
            if (!tag.IsValid || delta == 0) return false;

            // 显式计数
            int explicitOld = GetExplicitTagCount(tag);
            int explicitNew = Math.Max(0, explicitOld + delta);
            if (explicitNew == 0) _explicitCounts.Remove(tag);
            else _explicitCounts[tag] = explicitNew;
            int actualDelta = explicitNew - explicitOld;
            if (actualDelta == 0) return false;

            // 展开父链，逐级更新总计数
            bool flipped = false;
            var chain = GameplayTagManager.Instance.GetTagAndParents(tag);
            foreach (var t in chain)
            {
                int old = GetTagCount(t);
                int now = Math.Max(0, old + actualDelta);
                if (now == 0) _tagCounts.Remove(t);
                else _tagCounts[t] = now;

                OnAnyTagCountChanged?.Invoke(t, now);
                bool wasPresent = old > 0;
                bool isPresent = now > 0;
                if (wasPresent != isPresent)
                {
                    OnTagCountChanged?.Invoke(t, isPresent);
                    if (t.Equals(tag)) flipped = true;
                }
            }
            return flipped;
        }

        /// <summary>把当前拥有的（显式）标签填进容器。的简化。</summary>
        public void FillTagContainer(GameplayTagContainer outContainer)
        {
            if (outContainer == null) return;
            foreach (var kv in _explicitCounts)
                if (kv.Value > 0) outContainer.AddTag(kv.Key);
        }

        public void Reset()
        {
            _explicitCounts.Clear();
            _tagCounts.Clear();
        }
    }
}
