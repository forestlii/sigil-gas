// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 基于 CharacterMovementComponent 的实际移动实现。
// Unity 用 CharacterController（CMC 的等价物）：按当前移动状态速度移动 + 重力 + 落地检测 + 转向。

using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Character Movement System Component")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovementSystemComponent : MovementSystemComponent
    {
        [Header("物理")]
        [SerializeField] private float gravity = -20f;
        [Tooltip("ViewDirection 旋转模式下，朝向参考（通常是相机）。留空则用自身前方")]
        [SerializeField] private Transform viewReference;

        private CharacterController _cc;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        // 由 ApplyMovementSetting 从当前状态参数缓存
        private float _speed;
        private float _acceleration = 8f;
        private float _braking = 10f;
        private float _rotationSpeed = 12f;

        public Vector3 CurrentVelocity => _horizontalVelocity;
        public float CurrentSpeed => _speed;

        protected override void Awake()
        {
            base.Awake();
            _cc = GetComponent<CharacterController>();
        }

        // 当前移动状态参数应用到本组件（速度/加速度/转向速度）
        protected override void ApplyMovementSetting()
        {
            var s = GetMovementStateSetting();
            _speed = s.Speed;
            if (s.Acceleration > 0f) _acceleration = s.Acceleration;
            if (s.BrakingDeceleration > 0f) _braking = s.BrakingDeceleration;
            if (s.RotationInterpolationSpeed > 0f) _rotationSpeed = s.RotationInterpolationSpeed;
        }

        private void Update()
        {
            if (_cc == null) return;
            float dt = Time.deltaTime;

            // 落地检测 → LocomotionMode（写回状态总线）
            SetLocomotionMode(_cc.isGrounded ? MovementTags.LocomotionMode_Grounded : MovementTags.LocomotionMode_InAir);

            // 目标水平速度 = 输入方向 * 当前状态速度
            Vector3 input = GetInputDirection(); input.y = 0f;
            Vector3 desiredVel = input * _speed;

            // 朝目标速度加速/减速
            float rate = desiredVel.sqrMagnitude > 0.0001f ? _acceleration : _braking;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVel, rate * dt);

            // 重力
            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += gravity * dt;

            // 移动
            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            _cc.Move(motion);

            UpdateRotation(dt);
        }

        // 按旋转模式转向：VelocityDirection=朝移动方向，ViewDirection=朝视角方向
        private void UpdateRotation(float dt)
        {
            Vector3 face;
            if (GetRotationMode() == MovementTags.RotationMode_VelocityDirection)
            {
                if (_horizontalVelocity.sqrMagnitude < 0.01f) return;
                face = _horizontalVelocity;
            }
            else // ViewDirection
            {
                face = viewReference != null ? viewReference.forward : transform.forward;
            }
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(face.normalized, Vector3.up);
            // 指数平滑插值（帧率无关）
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_rotationSpeed * dt));
        }
    }
}
