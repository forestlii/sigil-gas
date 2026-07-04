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
                foreach (Match m in TagRegex.Matches(text))
                {
                    var tag = m.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(tag)) found.Add(tag);
                }
            }

            int added = 0;
            foreach (var tag in found)
                if (settings.AddTag(tag)) added++;
            if (added > 0) GameplayTagSettingsUtil.Save(settings);

            Debug.Log($"[Likeon GAS] 标签扫描：含 RequestTag 的脚本 {scanned} 个，发现标签 {found.Count} 个，新增 {added} 个到注册表（共 {settings.Count} 个）。");
            EditorUtility.DisplayDialog("Gameplay Tag 扫描",
                $"扫描含 RequestTag 的脚本：{scanned} 个\n发现标签：{found.Count} 个\n新增到注册表：{added} 个\n注册表现共：{settings.Count} 个", "OK");
        }
    }
}
