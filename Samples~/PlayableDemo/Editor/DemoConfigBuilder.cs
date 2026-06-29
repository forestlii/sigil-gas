// 编辑器脚本：一键把 demo 的默认配置烘成一个 DemoConfig.asset（全部子资产嵌为它的 sub-asset），
// 并把 demo 场景里 GASDemo.Config 指向它 —— 让"数据驱动 / 策划可在 Inspector 配"这条真正落地。
// 菜单：Likeon ▸ GAS ▸ Generate Demo Config Assets
// 批处理：Unity.exe -batchmode -projectPath ... -executeMethod GASDemo.Editor.DemoConfigBuilder.Generate -quit
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GASDemo.Editor
{
    public static class DemoConfigBuilder
    {
        [MenuItem("Likeon/GAS/Generate Demo Config Assets")]
        public static void Generate()
        {
            string demoFolder = FindDemoFolder();
            if (demoFolder == null) { Debug.LogError("[GASDemo] 找不到 demo 文件夹（DemoConfig.cs 所在目录）"); return; }

            string configPath = demoFolder + "/DemoConfig.asset";
            string scenePath = demoFolder + "/GASDemo.unity";

            // 1) 建配置资产：用 CreateDefault() 产出默认图，逐个嵌为 DemoConfig.asset 的 sub-asset
            var cfg = DemoConfig.CreateDefault();
            cfg.name = "DemoConfig";

            // 删旧资产（重生成幂等）
            if (AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath) != null)
                AssetDatabase.DeleteAsset(configPath);

            AssetDatabase.CreateAsset(cfg, configPath);
            foreach (var sub in cfg.EnumerateSubAssets())
            {
                if (sub == null) continue;
                sub.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(sub, cfg); // 子资产嵌入同一 .asset，交叉引用保留
            }
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            Debug.Log($"[GASDemo] 配置资产已生成: {configPath}");

            // 2) 场景：确保有 GASDemo，并把 Config 指向上面的资产
            var loaded = AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
            var scene = File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var host = Object.FindObjectOfType<GASDemo>();
            if (host == null) host = new GameObject("GASDemo").AddComponent<GASDemo>();
            host.Config = loaded;
            EditorUtility.SetDirty(host);

            bool ok = EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log(ok ? $"[GASDemo] 场景已接好 DemoConfig: {scenePath}" : "[GASDemo] 场景保存失败");
        }

        // 用 DemoConfig.cs 的资产路径反推 demo 文件夹（兼容 Samples~ 导入后的实际路径，含空格/版本号）
        private static string FindDemoFolder()
        {
            foreach (var guid in AssetDatabase.FindAssets("DemoConfig t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/DemoConfig.cs")) return Path.GetDirectoryName(path).Replace('\\', '/');
            }
            return null;
        }
    }
}
