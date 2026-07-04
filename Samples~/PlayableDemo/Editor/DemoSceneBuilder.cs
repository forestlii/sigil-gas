// 编辑器脚本：程序化生成 demo 场景（让 Unity 自己产出正确的 .unity YAML/GUID）。
// 批处理调用：Unity.exe -batchmode -projectPath ... -executeMethod Likeon.GAS.Sample.PlayableDemo.Editor.DemoSceneBuilder.Build -quit
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Likeon.GAS.Sample.PlayableDemo.Editor
{
    public static class DemoSceneBuilder
    {
        private const string ScenePath = "Assets/Demo/PlayableDemo.unity";

        [MenuItem("Sigil/GAS/Build Demo Scene")]
        public static void Build()
        {
            // 新场景（含默认 Main Camera + Directional Light）
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 挂上 demo 引导组件，Play 时自动构建整个可玩场景
            var host = new GameObject("PlayableDemo");
            host.AddComponent<PlayableDemo>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(ok ? $"[PlayableDemo] 场景已生成: {ScenePath}" : "[PlayableDemo] 场景保存失败");
        }
    }
}
