// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// Unity 没有原生 GameplayTag，这里用层级点分字符串实现，

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 层级化的游戏标签，例如 "State.Sliding"、"Movement.State.Sprint"。
    /// 子标签 "A.B.C" 命中（Matches）父标签 "A.B"。
    /// 用 struct + 字符串实现，可被 Unity 序列化、可在 Inspector 里编辑。
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string tagName;

        /// <summary>标签全名，如 "State.Combat.Attacking"。</summary>
        public string TagName => tagName ?? string.Empty;

        /// <summary>是否为有效（非空）标签。::IsValid()。</summary>
        public bool IsValid => !string.IsNullOrEmpty(tagName);

        private GameplayTag(string name)
        {
            tagName = name;
        }

        /// <summary>
        /// 请求（注册并获取）一个标签。
        /// 会在 <see cref="GameplayTagManager"/> 里登记，便于层级查询。
        /// </summary>
        public static GameplayTag RequestTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return default;
            var tag = new GameplayTag(name);
            GameplayTagManager.Instance.RegisterTag(tag);
            return tag;
        }

        /// <summary>
        /// 层级匹配：本标签等于 other，或本标签是 other 的子标签。
        ///::MatchesTag。例"A.B.C".MatchesTag("A.B") == true。
        /// </summary>
        public bool MatchesTag(GameplayTag other)
        {
            if (!IsValid || !other.IsValid) return false;
            if (tagName == other.tagName) return true;
            // 子标签：本标签以 "other." 为前缀
            return tagName.StartsWith(other.tagName + ".", StringComparison.Ordinal);
        }

        /// <summary>精确匹配：标签名完全相等。::MatchesTagExact。</summary>
        public bool MatchesTagExact(GameplayTag other)
        {
            return IsValid && other.IsValid && tagName == other.tagName;
        }

        /// <summary>
        /// 获取直接父标签。"A.B.C" -> "A.B"。无父级返回无效标签。
        ///::RequestDirectParent。
        /// </summary>
        public GameplayTag RequestDirectParent()
        {
            if (!IsValid) return default;
            int lastDot = tagName.LastIndexOf('.');
            return lastDot < 0 ? default : new GameplayTag(tagName.Substring(0, lastDot));
        }

        public bool Equals(GameplayTag other) => string.Equals(tagName, other.tagName, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => tagName == null ? 0 : tagName.GetHashCode();
        public override string ToString() => TagName;

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

        /// <summary>无效空标签。::EmptyTag。</summary>
        public static readonly GameplayTag None = default;
    }
}
