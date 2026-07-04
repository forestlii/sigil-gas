// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 样板脚本模板：Create 菜单里一键生成空的 GameplayAbility / GameplayCueNotify / ExecutionCalculation C# 脚本。
// 模板文件在 Editor/ScriptTemplates/*.cs.txt（用 #SCRIPTNAME# 占位，重命名即替换类名）。

using UnityEditor;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    internal static class SigilScriptTemplates
    {
        [MenuItem("Assets/Create/Sigil/GAS/Ability (C# Script)", priority = 80)]
        public static void CreateAbility() => Create("GameplayAbilityScript", "NewAbility.cs");

        [MenuItem("Assets/Create/Sigil/GAS/Cue Notify (C# Script)", priority = 81)]
        public static void CreateCueNotify() => Create("GameplayCueNotifyScript", "NewCueNotify.cs");

        [MenuItem("Assets/Create/Sigil/Combat/Execution Calculation (C# Script)", priority = 80)]
        public static void CreateExecution() => Create("ExecutionCalculationScript", "NewExecutionCalculation.cs");

        private static void Create(string templateName, string defaultFileName)
        {
            string path = FindTemplate(templateName);
            if (path == null)
            {
                Debug.LogError($"[Sigil] 找不到脚本模板 {templateName}.cs.txt（应在 Sigil 包的 Editor/ScriptTemplates/ 下）。");
                return;
            }
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(path, defaultFileName);
        }

        private static string FindTemplate(string templateName)
        {
            foreach (var guid in AssetDatabase.FindAssets(templateName))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith($"{templateName}.cs.txt")) return p;
            }
            return null;
        }
    }
}
