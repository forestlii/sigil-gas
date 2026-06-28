// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 运动动画的纯数学：方向角、四向/八向方向选择（带死区滞回）、倾身量。
// 全是无副作用的静态函数，便于单测。坐标约定：Unity Y 轴朝上，角色前方=+Z、右方=+X。

using UnityEngine;

namespace Likeon.GAS
{
    public static class LocomotionMath
    {
        /// <summary>
        /// 速度方向相对角色朝向的偏航角，单位度，范围 [-180,180]，正=向右、0=正前、±180=正后。
        /// </summary>
        public static float VelocityYawAngle(Vector3 worldVelocity, Quaternion facing)
        {
            Vector3 v = worldVelocity; v.y = 0f;
            if (v.sqrMagnitude < 1e-6f) return 0f;
            Vector3 fwd = facing * Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(fwd.normalized, v.normalized, Vector3.up);
        }

        /// <summary>把世界速度转到角色朝向空间（前=+Z，右=+X，去掉竖直分量）。</summary>
        public static Vector3 ToLocalPlanar(Vector3 worldVec, Quaternion facing)
        {
            Vector3 local = Quaternion.Inverse(facing) * worldVec;
            local.y = 0f;
            return local;
        }

        /// <summary>
        /// 从偏航角选四向。带死区滞回：若 useCurrent，当前方向是前/后时把对应死区翻倍，
        /// 避免在 45°/135° 分界处来回抖动。deadZone 典型取 ~10 度。
        /// </summary>
        public static EMovementDirection SelectCardinal(float angle, float deadZone, EMovementDirection current, bool useCurrent)
        {
            float fwdZone = deadZone, bwdZone = deadZone;
            if (useCurrent)
            {
                if (current == EMovementDirection.Forward) fwdZone *= 2f;
                else if (current == EMovementDirection.Backward) bwdZone *= 2f;
            }

            float abs = Mathf.Abs(angle);
            if (abs <= 45f + fwdZone) return EMovementDirection.Forward;
            if (abs >= 135f - bwdZone) return EMovementDirection.Backward;
            return angle > 0f ? EMovementDirection.Right : EMovementDirection.Left;
        }

        /// <summary>从偏航角选八向，同样对前/后方向做死区滞回。</summary>
        public static EMovementDirection8 SelectOctagonal(float angle, float deadZone, EMovementDirection8 current, bool useCurrent)
        {
            float fwdZone = deadZone, bwdZone = deadZone;
            if (useCurrent)
            {
                if (current == EMovementDirection8.Forward) fwdZone *= 2f;
                else if (current == EMovementDirection8.Backward) bwdZone *= 2f;
            }

            float abs = Mathf.Abs(angle);
            if (abs <= 22.5f + fwdZone) return EMovementDirection8.Forward;
            if (abs >= 157.5f - bwdZone) return EMovementDirection8.Backward;

            if (angle >= 22.5f && angle < 67.5f) return EMovementDirection8.ForwardRight;
            if (angle >= 67.5f && angle < 112.5f) return EMovementDirection8.Right;
            if (angle >= 112.5f && angle < 157.5f) return EMovementDirection8.BackwardRight;
            if (angle <= -22.5f && angle > -67.5f) return EMovementDirection8.ForwardLeft;
            if (angle <= -67.5f && angle > -112.5f) return EMovementDirection8.Left;
            return EMovementDirection8.BackwardLeft; // (-157.5, -112.5]
        }

        public static EMovementDirection Opposite(EMovementDirection d)
        {
            switch (d)
            {
                case EMovementDirection.Forward: return EMovementDirection.Backward;
                case EMovementDirection.Backward: return EMovementDirection.Forward;
                case EMovementDirection.Left: return EMovementDirection.Right;
                default: return EMovementDirection.Left;
            }
        }

        /// <summary>
        /// 相对加速度倾身量：把加速度转到角色朝向空间，按"加速用最大加速度、减速用最大制动"归一化到 [-1,1]。
        /// 返回 (x=前后, y=左右)。加速度与速度同向用 maxAccel，反向用 maxBraking。
        /// </summary>
        public static Vector2 RelativeAccelerationAmount(Vector3 acceleration, Vector3 velocity, Quaternion facing,
            float maxAcceleration, float maxBraking)
        {
            float max = Vector3.Dot(acceleration, velocity) >= 0f ? maxAcceleration : maxBraking;
            if (max <= Mathf.Epsilon) return Vector2.zero;

            Vector3 local = ToLocalPlanar(acceleration, facing) / max;
            Vector2 amount = new Vector2(local.z, local.x); // x=前后, y=左右
            return Vector2.ClampMagnitude(amount, 1f);
        }

        // ============================== View / AimOffset ==============================

        /// <summary>
        /// 视角相对角色朝向的偏航角，[-180,180]，正=向右看。视角与朝向的水平投影夹角
        /// （view.Yaw - actor.Yaw 归一化），这里用水平投影 SignedAngle 求得，规避欧拉万向锁。
        /// </summary>
        public static float ViewYawAngle(Quaternion viewRotation, Quaternion facing)
        {
            Vector3 view = viewRotation * Vector3.forward; view.y = 0f;
            Vector3 fwd = facing * Vector3.forward; fwd.y = 0f;
            if (view.sqrMagnitude < 1e-6f || fwd.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(fwd.normalized, view.normalized, Vector3.up);
        }

        /// <summary>
        /// 视角俯仰角，[-90,90]，正=向上看。角色一般直立（actor.Pitch≈0），故取视角自身俯仰，
        /// 取视角自身俯仰作为近似。
        /// </summary>
        public static float ViewPitchAngle(Quaternion viewRotation)
        {
            Vector3 view = viewRotation * Vector3.forward;
            return Mathf.Asin(Mathf.Clamp(view.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>俯仰归一化量 = 0.5 - pitch/180（向上看→0，向下看→1，平视→0.5）。</summary>
        public static float PitchToAmount(float pitchAngle) => 0.5f - pitchAngle / 180f;

        // ============================== 空中 ==============================

        /// <summary>
        /// 空中倾身量（x=前后, y=左右）：把世界速度转到角色朝向空间，除以参考速度，再乘竖直方向乘子
        /// （由调用方按竖直速度从曲线求得，用于上升↔下落平滑反向）。
        /// </summary>
        public static Vector2 InAirLeanAmount(Vector3 worldVelocity, Quaternion facing, float referenceSpeed, float verticalMultiplier)
        {
            if (referenceSpeed <= Mathf.Epsilon) return Vector2.zero;
            Vector3 local = ToLocalPlanar(worldVelocity, facing) / referenceSpeed * verticalMultiplier;
            return new Vector2(local.z, local.x); // x=前后, y=左右
        }

        /// <summary>
        /// 着地预测的 sweep 向量（米）：从角色位置沿"下压后的速度方向"伸出一段，长度随下落速度从近到远映射。
        /// 返回零向量表示当前不该预测（竖直速度未达阈值）。调用方据此从位置做 capsule/sphere cast。
        /// 阈值由原始 cm 单位换算成 m：触发 -2、映射区间 [-2,-40]→[1.5,20] 米，再乘角色缩放。
        /// </summary>
        public static Vector3 GroundPredictionSweep(Vector3 worldVelocity, float verticalSpeed, float scale)
        {
            const float verticalThreshold = -2f;   // 慢于此不预测
            const float minVerticalVel = -40f;
            const float maxVerticalVel = -2f;
            const float minSweep = 1.5f;
            const float maxSweep = 20f;

            if (verticalSpeed > verticalThreshold) return Vector3.zero;

            Vector3 dir = worldVelocity;
            dir.y = Mathf.Clamp(dir.y, minVerticalVel, maxVerticalVel);
            if (dir.sqrMagnitude < 1e-6f) return Vector3.zero;
            dir.Normalize();

            // value 在 [maxVerticalVel, minVerticalVel] 映射到 [minSweep, maxSweep]，越快越远
            float t = Mathf.Clamp01((verticalSpeed - maxVerticalVel) / (minVerticalVel - maxVerticalVel));
            float distance = Mathf.Lerp(minSweep, maxSweep, t) * Mathf.Max(scale, 0.0001f);
            return dir * distance;
        }
    }
}
