// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 运动动画的数据类型：移动方向枚举 + 每帧算出的运动状态（喂给 Animator 的驱动值）。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>四向移动方向（相对角色朝向）。</summary>
    public enum EMovementDirection { Forward, Backward, Left, Right }

    /// <summary>八向移动方向（相对角色朝向）。</summary>
    public enum EMovementDirection8 { Forward, ForwardRight, Right, BackwardRight, Backward, BackwardLeft, Left, ForwardLeft }

    /// <summary>
    /// 一帧的运动状态：由 <see cref="LocomotionAnimationDriver"/> 从移动数据算出，
    /// 既可直接读，也会按需写进 Animator 参数，供混合树消费。
    /// </summary>
    public struct LocomotionState
    {
        /// <summary>世界速度。</summary>
        public Vector3 Velocity;
        /// <summary>水平速率（米/秒）。</summary>
        public float Speed;
        /// <summary>是否在水平方向有速度。</summary>
        public bool HasVelocity;

        /// <summary>速度在角色朝向空间里的分量（前=+Z，右=+X）。</summary>
        public Vector3 LocalVelocity;
        /// <summary>速度方向相对朝向的偏航角，[-180,180]，正=向右。</summary>
        public float VelocityYawAngle;

        /// <summary>四向移动方向（带死区滞回，防边界抖动）。</summary>
        public EMovementDirection CardinalDirection;
        /// <summary>八向移动方向。</summary>
        public EMovementDirection8 OctagonalDirection;

        /// <summary>是否有移动输入。</summary>
        public bool HasInput;
        /// <summary>倾身量：x=前后（前为正），y=左右（右为正），各 [-1,1]。</summary>
        public Vector2 Lean;

        /// <summary>是否在地面。</summary>
        public bool Grounded;
        /// <summary>竖直速度（向上为正）。</summary>
        public float VerticalSpeed;
        /// <summary>是否上升中（跳跃）。</summary>
        public bool Jumping;
        /// <summary>是否下落中。</summary>
        public bool Falling;
        /// <summary>本帧刚落地（上一帧不在地面、本帧在地面），仅维持一帧。</summary>
        public bool JustLanded;
        /// <summary>到跳跃顶点的剩余时间（秒）；仅上升中有效，否则 0。</summary>
        public float TimeToJumpApex;
        /// <summary>持续下落时间（秒）；上升或落地后归零。</summary>
        public float FallingTime;
        /// <summary>着地预测是否命中可站立地面（需启用 GroundPrediction）。</summary>
        public bool HasPredictedGround;
        /// <summary>着地预测命中距离（米）；未命中或未启用为 -1。</summary>
        public float GroundDistance;

        // —— View / AimOffset（视角相对角色的偏移，喂瞄准混合）——
        /// <summary>视角偏航相对角色朝向，[-180,180]，正=向右看。</summary>
        public float ViewYawAngle;
        /// <summary>视角俯仰，[-180,180]，正=向上看（Pitch 取自旋转）。</summary>
        public float ViewPitchAngle;
        /// <summary>俯仰归一化量 = 0.5 - Pitch/180，落在 [0,1]，给 AimOffset BlendTree 的 Y 轴。</summary>
        public float ViewPitchAmount;
        /// <summary>视角偏航变化速度（度/秒），可驱动转身/aim 平滑。</summary>
        public float ViewYawSpeed;
    }

    /// <summary>
    /// 核心状态快照：从 <see cref="MovementSystemComponent"/> 读出的几个驱动动画分层的 GameplayTag，
    /// 以及"本帧相对上帧是否变化"的标志（core-state changed 检测）。
    /// </summary>
    public struct LocomotionCoreState
    {
        public GameplayTag LocomotionMode;
        public GameplayTag RotationMode;
        public GameplayTag MovementState;
        public GameplayTag MovementSet;

        public bool LocomotionModeChanged;
        public bool RotationModeChanged;
        public bool MovementStateChanged;
        public bool MovementSetChanged;

        /// <summary>任一核心状态本帧发生变化。</summary>
        public bool HasAnyChange =>
            LocomotionModeChanged || RotationModeChanged || MovementStateChanged || MovementSetChanged;
    }
}
