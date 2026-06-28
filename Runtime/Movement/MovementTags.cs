// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 移动系统的状态标签常量（字符串与源码一致）。

namespace Likeon.GAS
{
    /// <summary>移动系统的 GameplayTag 常量。</summary>
    public static class MovementTags
    {
        // 运动模式（所处环境）
        public static readonly GameplayTag LocomotionMode_None = GameplayTag.RequestTag("Movement.Locomotion.None");
        public static readonly GameplayTag LocomotionMode_Grounded = GameplayTag.RequestTag("Movement.Locomotion.Grounded");
        public static readonly GameplayTag LocomotionMode_InAir = GameplayTag.RequestTag("Movement.Locomotion.InAir");
        public static readonly GameplayTag LocomotionMode_Flying = GameplayTag.RequestTag("Movement.Locomotion.Flying");
        public static readonly GameplayTag LocomotionMode_Swimming = GameplayTag.RequestTag("Movement.Locomotion.Swimming");

        // 旋转模式
        public static readonly GameplayTag RotationMode_VelocityDirection = GameplayTag.RequestTag("Movement.Rotation.VelocityDirection");
        public static readonly GameplayTag RotationMode_ViewDirection = GameplayTag.RequestTag("Movement.Rotation.ViewDirection");

        // 移动状态（速度档）—— 框架预置只有这三个；下蹲/滑铲是项目层用机制配出来的
        public static readonly GameplayTag MovementState_Walk = GameplayTag.RequestTag("Movement.State.Walk");
        public static readonly GameplayTag MovementState_Jog = GameplayTag.RequestTag("Movement.State.Jog");
        public static readonly GameplayTag MovementState_Sprint = GameplayTag.RequestTag("Movement.State.Sprint");

        // 叠加模式
        public static readonly GameplayTag OverlayMode_None = GameplayTag.RequestTag("Movement.Overlay.None");
        public static readonly GameplayTag OverlayMode_Default = GameplayTag.RequestTag("Movement.Overlay.Default");
    }
}
