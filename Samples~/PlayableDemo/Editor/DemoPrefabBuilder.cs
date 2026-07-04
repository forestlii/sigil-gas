// 编辑器脚本：把 demo 从"运行时 AddComponent 构建"升级为"编辑器里配好的 Loadout + prefab + 场景"，
// 示范真实的"策划在 Inspector 配角色"工作流。本生成器分阶段产出：
//   ② Loadout 资产（PlayerLoadout/EnemyLoadout，引用 DemoConfig 的技能/属性集）→ 本步
//   ② prefab（玩家/敌人，组件齐 + prefab 内部引用接好 + ASC.initialLoadouts）→ 后续
//   ② 场景（地面/相机/prefab 实例/HUD）→ 后续
// 菜单：Sigil ▸ GAS ▸ Demo ▸ Build Loadouts / Build Prefabs / Build Scene
// 批处理：Unity.exe -batchmode -projectPath ... -executeMethod Likeon.GAS.Sample.PlayableDemo.Editor.DemoPrefabBuilder.BuildLoadouts -quit
using System.Collections.Generic;
using System.IO;
using Likeon.GAS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo.Editor
{
    public static class DemoPrefabBuilder
    {
        // ===================== 阶段② Loadout 资产 =====================
        [MenuItem("Sigil/GAS/Demo/Build Loadouts")]
        public static void BuildLoadouts()
        {
            string demoFolder = FindDemoFolder();
            if (demoFolder == null) { Debug.LogError("[PlayableDemo] 找不到 demo 文件夹（DemoConfig.cs 所在目录）"); return; }

            var cfg = LoadOrBuildConfig(demoFolder);
            if (cfg == null) { Debug.LogError("[PlayableDemo] DemoConfig.asset 缺失且无法生成；请先运行 Generate Demo Config Assets"); return; }

            BakeLoadout(cfg.BuildPlayerLoadout(), demoFolder + "/PlayerLoadout.asset");
            BakeLoadout(cfg.BuildEnemyLoadout(), demoFolder + "/EnemyLoadout.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayableDemo] Loadout 资产已生成: PlayerLoadout.asset / EnemyLoadout.asset");
        }

        private static void BakeLoadout(AbilityLoadout lo, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AbilityLoadout>(path) != null)
                AssetDatabase.DeleteAsset(path); // 重生成幂等
            AssetDatabase.CreateAsset(lo, path);
            EditorUtility.SetDirty(lo);
            AssetDatabase.ImportAsset(path);
        }

        // ===================== 阶段② Player/Enemy prefab =====================
        // 用共享构造器 DemoActorBuilder 在编辑器里建好结构（socket/武器/muzzle 子物体 + 同 prefab 组件引用 + 资产引用），
        // 再 PrefabUtility.SaveAsPrefabAsset —— Unity 自动序列化 prefab 内部引用，避免手工 SerializedProperty 接嵌套 entries。
        // ASC.initialLoadouts 接对应 loadout（实例化时 Awake 授予属性集+技能）。颜色不烘（运行时上，见 DemoActorBuilder.SetColor）。
        // prefab 进 Resources/ 子目录，供冒烟测试 Resources.Load 加载（§6.2）。
        [MenuItem("Sigil/GAS/Demo/Build Prefabs")]
        public static void BuildPrefabs()
        {
            string demoFolder = FindDemoFolder();
            if (demoFolder == null) { Debug.LogError("[PlayableDemo] 找不到 demo 文件夹"); return; }

            var cfg = LoadOrBuildConfig(demoFolder);
            if (cfg == null) { Debug.LogError("[PlayableDemo] DemoConfig.asset 缺失且无法生成"); return; }

            BuildLoadouts(); // 确保 loadout 资产在
            var playerLoadout = AssetDatabase.LoadAssetAtPath<AbilityLoadout>(demoFolder + "/PlayerLoadout.asset");
            var enemyLoadout = AssetDatabase.LoadAssetAtPath<AbilityLoadout>(demoFolder + "/EnemyLoadout.asset");

            string resFolder = demoFolder + "/Resources";
            if (!AssetDatabase.IsValidFolder(resFolder)) AssetDatabase.CreateFolder(demoFolder, "Resources");

            // 玩家 prefab
            var pRefs = DemoActorBuilder.BuildPlayer(cfg);
            if (playerLoadout != null) pRefs.ASC.InitialLoadouts.Add(playerLoadout);
            PrefabUtility.SaveAsPrefabAsset(pRefs.Root, resFolder + "/DemoPlayer.prefab");
            Object.DestroyImmediate(pRefs.Root);

            // 敌人 prefab
            var enemy = DemoActorBuilder.BuildEnemy(cfg);
            if (enemyLoadout != null) enemy.GetComponent<AbilitySystemComponent>().InitialLoadouts.Add(enemyLoadout);
            PrefabUtility.SaveAsPrefabAsset(enemy, resFolder + "/DemoEnemy.prefab");
            Object.DestroyImmediate(enemy);

            AssetDatabase.SaveAssets();
            Debug.Log("[PlayableDemo] prefab 已生成: Resources/DemoPlayer.prefab / DemoEnemy.prefab");
        }

        // ===================== 阶段②c 场景（玩家+敌人 prefab 实例接 PlayableDemo）=====================
        // 把 prefab 实例摆进场景（策划能在场景里看到/移动它们、改 prefab override），PlayableDemo 退化为薄编排：
        // 运行时只接 prefab 接不了的跨边界引用（相机 ViewSource/ThirdPersonCamera/HUD）+ 动态订阅（敌人变色/命中 Cue）+ 运行时上色。
        [MenuItem("Sigil/GAS/Demo/Build Scene")]
        public static void BuildScene()
        {
            string demoFolder = FindDemoFolder();
            if (demoFolder == null) { Debug.LogError("[PlayableDemo] 找不到 demo 文件夹"); return; }

            BuildPrefabs(); // 确保 loadout+prefab 在（链式：BuildPrefabs→BuildLoadouts）
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(demoFolder + "/Resources/DemoPlayer.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(demoFolder + "/Resources/DemoEnemy.prefab");
            if (playerPrefab == null || enemyPrefab == null) { Debug.LogError("[PlayableDemo] prefab 缺失，BuildScene 中止"); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 玩家实例
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0, 1, 0);
            var ctrl = player.GetComponent<DemoPlayerController>();

            // 3 敌人实例（前方扇形，便于演示锁定左右切换）
            var spawns = new[] { new Vector3(-3.5f, 1, 5), new Vector3(0f, 1, 6.5f), new Vector3(3.5f, 1, 5) };
            var enemyAscs = new List<AbilitySystemComponent>();
            foreach (var pos in spawns)
            {
                var en = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                en.transform.position = pos;
                enemyAscs.Add(en.GetComponent<AbilitySystemComponent>());
            }

            // PlayableDemo host：接好场景实例引用（adopt 模式）+ 配置资产
            var host = new GameObject("PlayableDemo").AddComponent<PlayableDemo>();
            host.Config = AssetDatabase.LoadAssetAtPath<DemoConfig>(demoFolder + "/DemoConfig.asset");
            host.ScenePlayer = ctrl;
            host.SceneEnemies = enemyAscs;
            EditorUtility.SetDirty(host);

            bool ok = EditorSceneManager.SaveScene(scene, demoFolder + "/PlayableDemo.unity");
            Debug.Log(ok
                ? "[PlayableDemo] 场景已生成: PlayableDemo.unity（玩家+3敌人 prefab 实例已接 PlayableDemo adopt 模式）"
                : "[PlayableDemo] 场景保存失败");
        }

        // 一键：loadout + prefab + 场景全套
        [MenuItem("Sigil/GAS/Demo/Build All (Loadouts + Prefabs + Scene)")]
        public static void BuildAll() => BuildScene();

        // ===================== 共用工具 =====================
        // DemoConfig.asset 在场则加载；不在则用 DemoConfigBuilder 烘一套再加载。
        private static DemoConfig LoadOrBuildConfig(string demoFolder)
        {
            string configPath = demoFolder + "/DemoConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
            if (cfg == null)
            {
                DemoConfigBuilder.Generate();
                cfg = AssetDatabase.LoadAssetAtPath<DemoConfig>(configPath);
            }
            return cfg;
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
