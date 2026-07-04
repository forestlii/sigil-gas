// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器：Sigil ▸ GAS ▸ Documentation —— 打开随包附带的使用文档。

using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Likeon.GAS.Editor
{
    internal static class SigilDocMenu
    {
        private const string DocRelPath = "Documentation~/Usage.md";

        [MenuItem("Sigil/GAS/Documentation", priority = 1)]
        public static void OpenDocumentation()
        {
            // 解析本包在磁盘上的真实路径（兼容 Packages/ 嵌入或 file: 引用），拼出文档路径。
            var info = PackageInfo.FindForAssembly(typeof(SigilDocMenu).Assembly);
            if (info != null)
            {
                var docPath = Path.Combine(info.resolvedPath, DocRelPath);
                if (File.Exists(docPath))
                {
                    Application.OpenURL("file:///" + docPath.Replace('\\', '/'));
                    return;
                }
            }

            // 兜底：找不到随包文档时给出提示（README / CHANGELOG 也含说明）。
            EditorUtility.DisplayDialog(
                "Sigil 文档 Documentation",
                "未找到随包文档 Documentation~/Usage.md（中文见 Usage.zh-CN.md）。\n包内 README.md 与 CHANGELOG.md 也含使用说明。",
                "OK");
        }
    }
}
