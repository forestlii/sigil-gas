// Copyright 2026 Likeon All Rights Reserved.
// 全局标签注册表与层级查询。
// Unity 版做轻量实现：维护已知标签集合，提供父链展开（用于 CountContainer 的父标签累加）。

using System.Collections.Generic;

namespace Likeon.GAS
{
    /// <summary>
    /// 全局标签管理器。负责登记运行时出现过的标签，并提供父链展开。
    ///的最小可用子集（不做编辑器 .ini 表，运行时按需注册）。
    /// </summary>
    public sealed class GameplayTagManager
    {
        private static GameplayTagManager _instance;
        public static GameplayTagManager Instance => _instance ??= new GameplayTagManager();

        private readonly HashSet<string> _registeredTags = new HashSet<string>();
        // 缓存：标签 -> 自身及所有父标签（含自己），避免重复 split。
        private readonly Dictionary<string, GameplayTag[]> _parentChainCache = new Dictionary<string, GameplayTag[]>();

        /// <summary>登记一个标签（幂等）。</summary>
        public void RegisterTag(GameplayTag tag)
        {
            if (tag.IsValid)
                _registeredTags.Add(tag.TagName);
        }

        /// <summary>该标签是否已登记。</summary>
        public bool IsRegistered(GameplayTag tag) => tag.IsValid && _registeredTags.Contains(tag.TagName);

        /// <summary>
        /// 返回标签自身 + 所有祖先标签。"A.B.C" -> [A.B.C, A.B, A]。
        /// 用于 <see cref="GameplayTagCountContainer"/> 在加入子标签时同时累加父标签计数，
        /// 从而让 HasTag("A") 在只显式加入 "A.B.C" 时也成立。
        /// </summary>
        public GameplayTag[] GetTagAndParents(GameplayTag tag)
        {
            if (!tag.IsValid) return System.Array.Empty<GameplayTag>();
            if (_parentChainCache.TryGetValue(tag.TagName, out var cached))
                return cached;

            var chain = new List<GameplayTag>();
            string name = tag.TagName;
            while (!string.IsNullOrEmpty(name))
            {
                chain.Add(GameplayTag.RequestTag(name));
                int lastDot = name.LastIndexOf('.');
                if (lastDot < 0) break;
                name = name.Substring(0, lastDot);
            }
            var arr = chain.ToArray();
            _parentChainCache[tag.TagName] = arr;
            return arr;
        }
    }
}
