// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 运动动画驱动：每帧从移动数据算出 LocomotionState（速度/方向/倾身/视角/空中），并读出移动系统的
// 核心状态标签（LocomotionMode/RotationMode/MovementState/MovementSet）。既可直接读，也按需写进
// Animator 参数，供混合树（idle/walk/run、方向、lean、aim offset、jump/fall）与状态机分层消费。
// 动画混合本身由宿主工程的 Animator Controller 负责；本组件只产出驱动值。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>把一个核心状态标签映射到 Animator 的 bool 参数：当前任一核心状态等于该标签时写 true。</summary>
    [Serializable]
    public struct LocomotionTagParameter
    {
        [Tooltip("关心的核心状态标签，如 Movement.State.Sprint")]
        public GameplayTag tag;
        [Tooltip("对应的 Animator bool 参数名")]
        public string boolParam;
    }

    [AddComponentMenu("Likeon/GAS/Locomotion Animation Driver")]
    public class LocomotionAnimationDriver : MonoBehaviour
    {
        [Header("来源")]
        [Tooltip("速度/着地来源（留空则在本物体或父级查找）")]
        [SerializeField] private CharacterController characterController;
        [Tooltip("朝向来源（留空用本物体 transform）")]
        [SerializeField] private Transform facing;
        [Tooltip("核心状态来源（留空则在本物体或父级查找）")]
        [SerializeField] private MovementSystemComponent movementSystem;
        [Tooltip("视角/瞄准来源（相机或控制旋转，驱动 AimOffset）；留空则不更新 View")]
        [SerializeField] private Transform aimSource;

        [Header("参数")]
        [Tooltip("方向选择死区（度），当前方向会翻倍以防边界抖动")]
        [SerializeField] private float directionDeadZone = 10f;
        [Tooltip("低于此速率视为静止（米/秒）")]
        [SerializeField] private float movingSpeedThreshold = 0.1f;
        [Tooltip("倾身平滑速度（越大越跟手）")]
        [SerializeField] private float leanSmoothing = 8f;
        [Tooltip("倾身归一化用的最大加速度 / 最大制动减速度（米/秒²）")]
        [SerializeField] private float maxAcceleration = 12f;
        [SerializeField] private float maxBraking = 20f;

        [Header("空中")]
        [Tooltip("用 Physics.gravity 求跳跃顶点时间；关闭则用下方覆盖值")]
        [SerializeField] private bool useGravityFromPhysics = true;
        [Tooltip("重力 Y（向下为负），useGravityFromPhysics 关闭时生效")]
        [SerializeField] private float gravityOverride = -9.81f;
        [Tooltip("空中倾身参考速度（米/秒）")]
        [SerializeField] private float inAirLeanReferenceSpeed = 3.5f;
        [Tooltip("空中倾身竖直乘子曲线（输入=竖直速度 m/s），用于上升↔下落平滑反向；留空则恒为 1")]
        [SerializeField] private AnimationCurve inAirLeanVerticalMultiplier = AnimationCurve.Constant(-20f, 20f, 1f);

        [Header("着地预测")]
        [Tooltip("启用着地预测（下落时向前下方扫描可站立地面）")]
        [SerializeField] private bool enableGroundPrediction = false;
        [SerializeField] private LayerMask groundPredictionMask = ~0;
        [Tooltip("预测球扫半径（米）")]
        [SerializeField] private float groundPredictionRadius = 0.3f;
        [Tooltip("可站立地面的法线 Y 阈值（cos 坡度角），0.7≈45°")]
        [SerializeField] private float walkableFloorCos = 0.7f;

        [Header("Animator（可选）")]
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string directionParam = "Direction";
        [SerializeField] private string velocityYawParam = "VelocityYaw";
        [SerializeField] private string leanForwardParam = "LeanForward";
        [SerializeField] private string leanRightParam = "LeanRight";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
        [SerializeField] private string movingParam = "Moving";
        [SerializeField] private string jumpingParam = "Jumping";
        [SerializeField] private string fallingParam = "Falling";
        [SerializeField] private string viewYawParam = "ViewYaw";
        [SerializeField] private string viewPitchParam = "ViewPitch";
        [SerializeField] private string pitchAmountParam = "PitchAmount";
        [Tooltip("核心状态标签 → Animator bool 参数（如 Sprint→\"Sprinting\"）")]
        [SerializeField] private LocomotionTagParameter[] coreStateParams;

        public LocomotionState State => _state;
        public LocomotionCoreState CoreState => _coreState;

        private LocomotionState _state;
        private LocomotionCoreState _coreState;
        private Vector3 _prevVelocity;
        private bool _hasPrev;
        private bool _coreStateInit;

        private Transform Facing => facing != null ? facing : transform;

        private void Awake()
        {
            if (characterController == null) characterController = GetComponentInParent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (movementSystem == null) movementSystem = GetComponentInParent<MovementSystemComponent>();
            _state.CardinalDirection = EMovementDirection.Forward;
            _state.OctagonalDirection = EMovementDirection8.Forward;
            _state.GroundDistance = -1f;
        }

        private void Update()
        {
            Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;
            bool grounded = characterController == null || characterController.isGrounded;
            bool hasInput = new Vector2(velocity.x, velocity.z).sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;

            if (movementSystem != null)
                UpdateCoreState(movementSystem.GetLocomotionMode(), movementSystem.GetRotationMode(),
                    movementSystem.GetMovementState(), movementSystem.GetMovementSet());

            UpdateState(velocity, Facing.rotation, grounded, hasInput, transform.position, Time.deltaTime);

            if (aimSource != null)
                UpdateView(aimSource.rotation, Time.deltaTime);

            WriteAnimator();
        }

        /// <summary>
        /// 核心计算：从速度/朝向/着地算出本帧 LocomotionState（含死区滞回、地面/空中倾身、空中状态、着地预测）。
        /// 可被测试直接驱动。worldPosition 用于着地预测的 sweep 起点。
        /// </summary>
        public void UpdateState(Vector3 worldVelocity, Quaternion facingRotation, bool grounded, bool hasInput,
            Vector3 worldPosition, float deltaTime)
        {
            bool wasMoving = _state.HasVelocity;
            bool wasGrounded = _state.Grounded;

            _state.Velocity = worldVelocity;
            Vector3 horizontal = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            _state.Speed = horizontal.magnitude;
            _state.HasVelocity = _state.Speed > movingSpeedThreshold;
            _state.HasInput = hasInput;

            _state.LocalVelocity = LocomotionMath.ToLocalPlanar(worldVelocity, facingRotation);
            _state.VelocityYawAngle = LocomotionMath.VelocityYawAngle(worldVelocity, facingRotation);

            // 静止时保持上一帧方向，避免归零抖动；移动时用滞回选择
            if (_state.HasVelocity)
            {
                _state.CardinalDirection = LocomotionMath.SelectCardinal(
                    _state.VelocityYawAngle, directionDeadZone, _state.CardinalDirection, wasMoving);
                _state.OctagonalDirection = LocomotionMath.SelectOctagonal(
                    _state.VelocityYawAngle, directionDeadZone, _state.OctagonalDirection, wasMoving);
            }

            // 空中状态（先于倾身，倾身要按地面/空中分流）
            _state.Grounded = grounded;
            _state.VerticalSpeed = worldVelocity.y;
            _state.JustLanded = grounded && !wasGrounded;

            float gravity = useGravityFromPhysics ? Physics.gravity.y : gravityOverride;
            if (!grounded)
            {
                if (worldVelocity.y > 0.01f)
                {
                    _state.Jumping = true;
                    _state.Falling = false;
                    // gravity<0：-vy/gravity = vy/|g| > 0，到顶点剩余时间
                    _state.TimeToJumpApex = gravity < -1e-3f ? (-worldVelocity.y) / gravity : 0f;
                    _state.FallingTime = 0f;
                }
                else
                {
                    _state.Jumping = false;
                    _state.Falling = true;
                    _state.TimeToJumpApex = 0f;
                    _state.FallingTime += deltaTime;
                }
            }
            else
            {
                _state.Jumping = false;
                _state.Falling = false;
                _state.TimeToJumpApex = 0f;
                _state.FallingTime = 0f;
            }

            // 倾身：地面用加速度差分，空中用速度方向 + 竖直乘子
            if (deltaTime > 1e-5f)
            {
                Vector2 target;
                if (grounded)
                {
                    Vector3 acceleration = _hasPrev ? (worldVelocity - _prevVelocity) / deltaTime : Vector3.zero;
                    target = LocomotionMath.RelativeAccelerationAmount(acceleration, worldVelocity, facingRotation, maxAcceleration, maxBraking);
                }
                else
                {
                    float mult = inAirLeanVerticalMultiplier != null ? inAirLeanVerticalMultiplier.Evaluate(_state.VerticalSpeed) : 1f;
                    target = LocomotionMath.InAirLeanAmount(worldVelocity, facingRotation, inAirLeanReferenceSpeed, mult);
                }
                float t = 1f - Mathf.Exp(-Mathf.Max(0f, leanSmoothing) * deltaTime);
                _state.Lean = Vector2.Lerp(_state.Lean, target, t);
            }
            _prevVelocity = worldVelocity;
            _hasPrev = true;

            // 着地预测（仅空中且启用）
            if (!grounded && enableGroundPrediction)
                RefreshGroundPrediction(worldVelocity, worldPosition);
            else
            {
                _state.HasPredictedGround = false;
                _state.GroundDistance = -1f;
            }
        }

        /// <summary>着地预测：沿下压速度方向球扫，命中可站立地面则记录距离。</summary>
        private void RefreshGroundPrediction(Vector3 worldVelocity, Vector3 worldPosition)
        {
            float scale = transform.lossyScale.y;
            Vector3 sweep = LocomotionMath.GroundPredictionSweep(worldVelocity, _state.VerticalSpeed, scale);
            if (sweep.sqrMagnitude < 1e-6f)
            {
                _state.HasPredictedGround = false;
                _state.GroundDistance = -1f;
                return;
            }

            if (Physics.SphereCast(worldPosition, groundPredictionRadius, sweep.normalized, out RaycastHit hit,
                    sweep.magnitude, groundPredictionMask, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= walkableFloorCos)
            {
                _state.HasPredictedGround = true;
                _state.GroundDistance = hit.distance;
            }
            else
            {
                _state.HasPredictedGround = false;
                _state.GroundDistance = -1f;
            }
        }

        /// <summary>更新视角/瞄准偏移（相对角色朝向的 yaw/pitch）。可被测试直接驱动。</summary>
        public void UpdateView(Quaternion viewRotation, float deltaTime)
        {
            float yaw = LocomotionMath.ViewYawAngle(viewRotation, Facing.rotation);
            float pitch = LocomotionMath.ViewPitchAngle(viewRotation);
            _state.ViewYawSpeed = deltaTime > 1e-5f ? Mathf.DeltaAngle(_state.ViewYawAngle, yaw) / deltaTime : 0f;
            _state.ViewYawAngle = yaw;
            _state.ViewPitchAngle = pitch;
            _state.ViewPitchAmount = Mathf.Clamp01(LocomotionMath.PitchToAmount(pitch));
        }

        /// <summary>更新核心状态快照并算出各项 changed 标志（首帧不算变化）。可被测试直接喂标签。</summary>
        public void UpdateCoreState(GameplayTag locomotionMode, GameplayTag rotationMode,
            GameplayTag movementState, GameplayTag movementSet)
        {
            if (_coreStateInit)
            {
                _coreState.LocomotionModeChanged = locomotionMode != _coreState.LocomotionMode;
                _coreState.RotationModeChanged = rotationMode != _coreState.RotationMode;
                _coreState.MovementStateChanged = movementState != _coreState.MovementState;
                _coreState.MovementSetChanged = movementSet != _coreState.MovementSet;
            }
            else
            {
                _coreState.LocomotionModeChanged = false;
                _coreState.RotationModeChanged = false;
                _coreState.MovementStateChanged = false;
                _coreState.MovementSetChanged = false;
                _coreStateInit = true;
            }

            _coreState.LocomotionMode = locomotionMode;
            _coreState.RotationMode = rotationMode;
            _coreState.MovementState = movementState;
            _coreState.MovementSet = movementSet;
        }

        /// <summary>判断某标签是否等于当前任一核心状态（供 coreStateParams 映射用）。</summary>
        private bool CoreStateHas(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return tag == _coreState.LocomotionMode || tag == _coreState.RotationMode
                || tag == _coreState.MovementState || tag == _coreState.MovementSet;
        }

        /// <summary>读 Animator 暴露的动画曲线/参数值（Clip 导入设置勾 Curves 或有同名 float 参数）。</summary>
        public float GetAnimatorCurve(string curveName)
            => (animator != null && !string.IsNullOrEmpty(curveName)) ? animator.GetFloat(curveName) : 0f;

        /// <summary>同 GetAnimatorCurve 并 clamp 到 [0,1]。</summary>
        public float GetAnimatorCurveClamped01(string curveName) => Mathf.Clamp01(GetAnimatorCurve(curveName));

        private void WriteAnimator()
        {
            if (animator == null) return;
            if (!string.IsNullOrEmpty(speedParam)) animator.SetFloat(speedParam, _state.Speed);
            if (!string.IsNullOrEmpty(directionParam)) animator.SetInteger(directionParam, (int)_state.CardinalDirection);
            if (!string.IsNullOrEmpty(velocityYawParam)) animator.SetFloat(velocityYawParam, _state.VelocityYawAngle);
            if (!string.IsNullOrEmpty(leanForwardParam)) animator.SetFloat(leanForwardParam, _state.Lean.x);
            if (!string.IsNullOrEmpty(leanRightParam)) animator.SetFloat(leanRightParam, _state.Lean.y);
            if (!string.IsNullOrEmpty(groundedParam)) animator.SetBool(groundedParam, _state.Grounded);
            if (!string.IsNullOrEmpty(verticalSpeedParam)) animator.SetFloat(verticalSpeedParam, _state.VerticalSpeed);
            if (!string.IsNullOrEmpty(movingParam)) animator.SetBool(movingParam, _state.HasVelocity);
            if (!string.IsNullOrEmpty(jumpingParam)) animator.SetBool(jumpingParam, _state.Jumping);
            if (!string.IsNullOrEmpty(fallingParam)) animator.SetBool(fallingParam, _state.Falling);
            if (!string.IsNullOrEmpty(viewYawParam)) animator.SetFloat(viewYawParam, _state.ViewYawAngle);
            if (!string.IsNullOrEmpty(viewPitchParam)) animator.SetFloat(viewPitchParam, _state.ViewPitchAngle);
            if (!string.IsNullOrEmpty(pitchAmountParam)) animator.SetFloat(pitchAmountParam, _state.ViewPitchAmount);

            if (coreStateParams != null)
            {
                for (int i = 0; i < coreStateParams.Length; i++)
                {
                    var p = coreStateParams[i];
                    if (!string.IsNullOrEmpty(p.boolParam))
                        animator.SetBool(p.boolParam, CoreStateHas(p.tag));
                }
            }
        }
    }
}
