// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：扫描工程脚本里的 RequestTag("...") 字面量，一键补进标签注册表。

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    public static class GameplayTagScanner
    {
        // 匹配 RequestTag("某.标签") / RequestTag( "..." )
        private static readonly Regex TagRegex = new Regex("RequestTag\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        [MenuItem("Sigil/GAS/Scan Project for Gameplay Tags")]
        public static void Scan()
        {
            var settings = GameplayTagSettingsUtil.GetOrCreate();
            var found = new HashSet<string>();
            int scanned = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;

                var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (mono == null) continue;
                var text = mono.text;
                if (string.IsNullOrEmpty(text) || text.IndexOf("RequestTag", System.StringComparison.Ordinal) < 0) continue;

                scanned++;
                found.UnionWith(ExtractTags(text));
            }

            int added = 0;
            foreach (var tag in found)
                if (settings.AddTag(tag)) added++;
            if (added > 0) GameplayTagSettingsUtil.Save(settings);

            Debug.Log($"[Sigil] 标签扫描：含 RequestTag 的脚本 {scanned} 个，发现标签 {found.Count} 个，新增 {added} 个到注册表（共 {settings.Count} 个）。");
            EditorUtility.DisplayDialog("Gameplay Tag 扫描",
                $"扫描含 RequestTag 的脚本：{scanned} 个\n发现标签：{found.Count} 个\n新增到注册表：{added} 个\n注册表现共：{settings.Count} 个", "OK");
        }

        /// <summary>从一段 C# 源码提取 RequestTag("...") 的标签名。**先剥注释**（`//` 行注释、`/* */` 块注释），
        /// 避免把注释掉的 RequestTag 也扫进来；字符串字面量原样保留（其内的 // /* 不当注释）。</summary>
        public static HashSet<string> ExtractTags(string source)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(source)) return set;
            foreach (Match m in TagRegex.Matches(StripComments(source)))
            {
                var tag = m.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(tag)) set.Add(tag);
            }
            return set;
        }

        /// <summary>把 `//` 行注释与 `/* */` 块注释替换掉，但**保留字符串/字符字面量原文**
        /// （字面量内部的 `//`、`/*` 不算注释）。字符状态机，够用于本扫描器（不追求完整 C# 词法）。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                // 行注释 //
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    i += 2;
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }
                // 块注释 /* */
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i += 2; // 跳过 */（未闭合时越界，外层 while 会收住）
                    continue;
                }
                // 字符串 "..." / 字符 '...'：整段原样保留，内部转义 \ 跳一位
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char d = src[i];
                        sb.Append(d);
                        if (d == '\\' && i + 1 < n) { sb.Append(src[i + 1]); i += 2; continue; }
                        i++;
                        if (d == quote) break;
                    }
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
