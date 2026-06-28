// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 标签注册表：编辑器选择器与集中管理的数据源。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 已注册的 GameplayTag 主表。编辑器的标签选择器/管理页从这里读写。
    /// 运行时 RequestTag 仍可用任意字符串，本表只为编辑期提供下拉候选与集中管理。
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagsSettings", menuName = "Likeon/GAS/Gameplay Tags Settings")]
    public class GameplayTagsSettings : ScriptableObject
    {
        [SerializeField] private List<string> tags = new List<string>();

        public IReadOnlyList<string> Tags => tags;
        public int Count => tags.Count;

        public bool Contains(string tag) => !string.IsNullOrEmpty(tag) && tags.Contains(tag);

        /// <summary>新增标签（去重 + 排序）。成功返回 true。</summary>
        public bool AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            tag = tag.Trim();
            if (tags.Contains(tag)) return false;
            tags.Add(tag);
            tags.Sort(StringComparer.Ordinal);
            return true;
        }

        public bool RemoveTag(string tag) => tags.Remove(tag);
    }
}
