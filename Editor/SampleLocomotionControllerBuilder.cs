// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 编辑器工具：一键生成一个与 LocomotionAnimationDriver 参数对齐的示例分层 Animator Controller。
// 菜单：Likeon ▸ GAS ▸ Samples ▸ Create Locomotion Animator Controller
//
// 示例只搭「参数 + 分层 + 状态机/混合树骨架」——BlendTree 的动画 motion 槽**留空**（随包不含美术）。
// 宿主工程把自己的 idle / 八向 walk·run / jump / fall / aim 动画填进对应空槽即可驱动起来。
// 这样交付的是「可复用的生成器」而非预烘焙的 .controller（避免 GUID / 版本兼容隐患）。

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Likeon.GAS.Editor
{
    public static class SampleLocomotionControllerBuilder
    {
        private const string DefaultPath = "Assets/SampleLocomotionController.controller";

        [MenuItem("Likeon/GAS/Samples/Create Locomotion Animator Controller", priority = 50)]
        public static void CreateAtDefault()
        {
            var ctrl = Build(DefaultPath);
            Selection.activeObject = ctrl;
            EditorGUIUtility.PingObject(ctrl);
        }

        /// <summary>batchmode 入口（-executeMethod 用，无 GUI 副作用）。</summary>
        public static void RunBatch() => Build(DefaultPath);

        /// <summary>
        /// 程序化生成示例分层 Animator Controller 到 assetPath 并返回。
        /// 参数严格对齐 <see cref="LocomotionAnimationDriver"/>.WriteAnimator，使数据驱动层开箱即用。
        /// </summary>
        public static AnimatorController Build(string assetPath)
        {
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(assetPath);

            // —— 参数（对齐 LocomotionAnimationDriver.WriteAnimator 的 13 个驱动值）——
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Direction", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("VelocityYaw", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("LeanForward", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("LeanRight", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jumping", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Falling", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("ViewYaw", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("ViewPitch", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("PitchAmount", AnimatorControllerParameterType.Float);
            // 注：核心状态标签 bool（coreStateParams，如 Sprinting）由买家按需添加并在 driver 上配映射。

            BuildBaseLayer(ctrl);
            BuildAimLayer(ctrl);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SampleLocomotionController] 生成完成：params={ctrl.parameters.Length} layers={ctrl.layers.Length} → {assetPath}");
            return ctrl;
        }

        // Base Layer：地面 locomotion 2D 混合树 + 空中 Jump/Fall，按 bool 标签过渡。
        private static void BuildBaseLayer(AnimatorController ctrl)
        {
            var sm = ctrl.layers[0].stateMachine;

            // 地面：2D 方向混合（VelocityYaw 方向 × Speed 速率）。子节点 motion 留空，买家填 idle + 八向 walk/run。
            var locoTree = new BlendTree
            {
                name = "Grounded_Locomotion",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "VelocityYaw",
                blendParameterY = "Speed",
            };
            AssetDatabase.AddObjectToAsset(locoTree, ctrl);
            locoTree.AddChild(null, new Vector2(0f, 0f));      // idle（中心）
            locoTree.AddChild(null, new Vector2(0f, 1f));      // forward
            locoTree.AddChild(null, new Vector2(45f, 1f));     // forward-right
            locoTree.AddChild(null, new Vector2(90f, 1f));     // right
            locoTree.AddChild(null, new Vector2(135f, 1f));    // backward-right
            locoTree.AddChild(null, new Vector2(180f, 1f));    // backward
            locoTree.AddChild(null, new Vector2(-135f, 1f));   // backward-left
            locoTree.AddChild(null, new Vector2(-90f, 1f));    // left
            locoTree.AddChild(null, new Vector2(-45f, 1f));    // forward-left

            var grounded = sm.AddState("Grounded");
            grounded.motion = locoTree;
            sm.defaultState = grounded;

            var jump = sm.AddState("Jump");   // motion 留空：起跳/上升动画
            var fall = sm.AddState("Fall");   // motion 留空：下落动画

            AddBoolTransition(grounded, jump, "Jumping");
            AddBoolTransition(grounded, fall, "Falling");
            AddBoolTransition(jump, fall, "Falling");
            AddBoolTransition(jump, grounded, "Grounded");
            AddBoolTransition(fall, grounded, "Grounded");
        }

        // 上半身瞄准叠加层：Override + AvatarMask 占位 + AimOffset 混合树（ViewYaw × ViewPitch）。
        private static void BuildAimLayer(AnimatorController ctrl)
        {
            // AvatarMask 占位（默认全身 enabled）。买家在 mask 上禁用下半身即得「上半身瞄准、下半身照常移动」。
            var mask = new AvatarMask { name = "UpperBody_AimMask" };
            AssetDatabase.AddObjectToAsset(mask, ctrl);

            ctrl.AddLayer("UpperBody_AimOffset");
            var layers = ctrl.layers;
            var aim = layers[layers.Length - 1];
            aim.defaultWeight = 1f;
            aim.blendingMode = AnimatorLayerBlendingMode.Override;
            aim.avatarMask = mask;
            ctrl.layers = layers; // layers 是副本，改完写回

            var aimTree = new BlendTree
            {
                name = "AimOffset",
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "ViewYaw",
                blendParameterY = "ViewPitch",
            };
            AssetDatabase.AddObjectToAsset(aimTree, ctrl);
            aimTree.AddChild(null, new Vector2(0f, 0f));     // 平视中心
            aimTree.AddChild(null, new Vector2(-90f, 0f));   // 左
            aimTree.AddChild(null, new Vector2(90f, 0f));    // 右
            aimTree.AddChild(null, new Vector2(0f, 90f));    // 上
            aimTree.AddChild(null, new Vector2(0f, -90f));   // 下

            var aimSm = ctrl.layers[ctrl.layers.Length - 1].stateMachine;
            var aimState = aimSm.AddState("AimOffset");
            aimState.motion = aimTree;
            aimSm.defaultState = aimState;
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string boolParam)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.AddCondition(AnimatorConditionMode.If, 0f, boolParam);
        }
    }
}
