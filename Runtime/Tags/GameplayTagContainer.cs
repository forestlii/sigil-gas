// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一组标签的集合，支持层级匹配查询。

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 标签容器：一组 GameplayTag。
    /// HasTag 走层级匹配（容器里有 "A.B.C" 时 HasTag("A.B") 为真），HasTagExact 走精确匹配。
    /// </summary>
    [Serializable]
    public class GameplayTagContainer : IEnumerable<GameplayTag>
    {
        [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();

        public int Count => tags.Count;
        public IReadOnlyList<GameplayTag> Tags => tags;

        public GameplayTagContainer() { }

        public GameplayTagContainer(IEnumerable<GameplayTag> source)
        {
            if (source != null)
                foreach (var t in source) AddTag(t);
        }

        /// <summary>加入一个标签（去重，按精确名）。</summary>
        public void AddTag(GameplayTag tag)
        {
            if (tag.IsValid && !tags.Contains(tag))
                tags.Add(tag);
        }

        /// <summary>移除一个标签（精确）。</summary>
        public bool RemoveTag(GameplayTag tag) => tags.Remove(tag);

        public void AppendTags(GameplayTagContainer other)
        {
            if (other == null) return;
            foreach (var t in other.tags) AddTag(t);
        }

        public void Clear() => tags.Clear();

        /// <summary>
        /// 层级匹配：本容器里是否存在某标签匹配（等于或子级）给定标签。
        ///::HasTag。例：容器含 "A.B.C"，HasTag("A.B") == true。
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i].MatchesTag(tag)) return true;
            return false;
        }

        /// <summary>精确匹配：本容器里是否存在与给定标签同名的标签。</summary>
        public bool HasTagExact(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i].MatchesTagExact(tag)) return true;
            return false;
        }

        /// <summary>本容器是否包含 other 里的任意一个标签（层级）。</summary>
        public bool HasAny(GameplayTagContainer other)
        {
            if (other == null) return false;
            foreach (var t in other.tags)
                if (HasTag(t)) return true;
            return false;
        }

        /// <summary>本容器是否包含 other 里的全部标签（层级）。空 other 视为 true。</summary>
        public bool HasAll(GameplayTagContainer other)
        {
            if (other == null || other.Count == 0) return true;
            foreach (var t in other.tags)
                if (!HasTag(t)) return false;
            return true;
        }

        public bool IsEmpty => tags.Count == 0;

        public IEnumerator<GameplayTag> GetEnumerator() => tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => tags.GetEnumerator();

        public override string ToString() => "(" + string.Join(", ", tags) + ")";
    }
}
