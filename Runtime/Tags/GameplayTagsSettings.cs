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
    [CreateAssetMenu(fileName = "GameplayTagsSettings", menuName = "Sigil/GAS/Gameplay Tags Settings")]
    public class GameplayTagsSettings : ScriptableObject
    {
        [SerializeField] private List<string> tags = new List<string>();

        public IReadOnlyList<string> Tags => tags;
        public int Count => tags.Count;

        public bool Contains(string tag) => !string.IsNullOrEmpty(tag) && tags.Contains(tag);

        /// <summary>新增标签（去重 + 排序）。成功返回 true；格式非法或已存在返回 false。</summary>
        public bool AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            tag = tag.Trim();
            if (!IsValidTagName(tag)) return false; // 非法格式（空段/引号/非法字符）挡在注册表外，避免击穿常量生成器
            if (tags.Contains(tag)) return false;
            tags.Add(tag);
            tags.Sort(StringComparer.Ordinal);
            return true;
        }

        /// <summary>
        /// 校验标签名格式：点分段，每段非空、只含字母/数字/下划线/连字符（对齐 UE tag 命名字符集）。
        /// 拦截 <c>A..B</c> / <c>.A</c> / <c>A.</c> / 含引号或反斜杠等会生成不可编译常量的名字。
        /// </summary>
        public static bool IsValidTagName(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            var parts = tag.Split('.');
            foreach (var p in parts)
            {
                if (p.Length == 0) return false; // 空段
                foreach (var c in p)
                    if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
            }
            return true;
        }

        public bool RemoveTag(string tag) => tags.Remove(tag);
    }
}
