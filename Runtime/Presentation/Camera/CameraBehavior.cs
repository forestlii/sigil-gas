// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 相机行为：每帧算出一个相机视图（位置/朝向/FOV），并按 AnimationCurve 推进自己的混入权重。
// 多个相机行为压在混合栈里，由栈把它们的视图按权重融合成最终相机姿态。

using System;
using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>一个相机行为产出的视图，供混合栈融合。</summary>
    [Serializable]
    public struct CameraView
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfView;

        public static CameraView Default => new CameraView { Rotation = Quaternion.identity, FieldOfView = 60f };

        /// <summary>把 from 向 to 融合 tTowardTo 比例，返回结果（不改入参）。</summary>
        public static CameraView Mix(CameraView from, CameraView to, float tTowardTo)
        {
            if (tTowardTo <= 0f) return from;
            if (tTowardTo >= 1f) return to;
            return new CameraView
            {
                Position = Vector3.Lerp(from.Position, to.Position, tTowardTo),
                Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, tTowardTo),
                FieldOfView = Mathf.Lerp(from.FieldOfView, to.FieldOfView, tTowardTo)
            };
        }
    }

    /// <summary>
    /// 相机行为基类。子类实现 <see cref="Compute"/> 算出视图。
    /// 混入权重由一条 AnimationCurve 在 BlendInDuration 内从 0 爬到 1（栈用此权重融合）。
    /// </summary>
    [Serializable]
    public abstract class CameraBehavior
    {
        [Tooltip("可被 gameplay 查询的相机类型标签（如瞄准）")]
        public GameplayTag CameraTag;
        [Range(5f, 170f)] public float FieldOfView = 60f;

        [Tooltip("从压入到完全生效的时长(秒)")]
        public float BlendInDuration = 0.5f;
        [Tooltip("混入权重随归一化时间的曲线（默认平滑进出）")]
        public AnimationCurve BlendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [NonSerialized] private CameraView _view;
        [NonSerialized] private float _elapsed;
        [NonSerialized] private float _weight;

        /// <summary>本行为当前的视图。</summary>
        public CameraView View => _view;
        /// <summary>本行为当前的混入权重 [0,1]。</summary>
        public float Weight => _weight;

        /// <summary>压入栈时调用：重置混入进度。</summary>
        public virtual void OnEnter()
        {
            _elapsed = 0f;
            _weight = 0f;
            _view = CameraView.Default;
        }

        /// <summary>移出栈时调用。</summary>
        public virtual void OnExit() { }

        /// <summary>每帧推进：算视图 + 按曲线更新权重。</summary>
        public void Tick(Transform target, float deltaTime)
        {
            _view = Compute(target, deltaTime);
            if (BlendInDuration > 0f)
            {
                _elapsed += deltaTime;
                float normalized = Mathf.Clamp01(_elapsed / BlendInDuration);
                _weight = Mathf.Clamp01(BlendCurve.Evaluate(normalized));
            }
            else
            {
                _weight = 1f;
            }
        }

        /// <summary>直接设定权重（重新压入已有行为时承接旧权重，避免跳变）。</summary>
        public void SetWeight(float weight)
        {
            _weight = Mathf.Clamp01(weight);
            _elapsed = BlendInDuration * _weight; // 近似回填进度
        }

        /// <summary>子类算出本帧视图。</summary>
        protected abstract CameraView Compute(Transform target, float deltaTime);
    }

    /// <summary>第三人称相机：绕轴心的弹簧臂 + 碰撞拉近。</summary>
    [Serializable]
    public sealed class ThirdPersonCameraBehavior : CameraBehavior
    {
        [Header("第三人称")]
        public Vector3 PivotOffset = new Vector3(0f, 1.6f, 0f);
        public float ArmLength = 4f;
        public Vector2 PitchClampDegrees = new Vector2(-60f, 70f);

        [Header("碰撞拉近")]
        public LayerMask CollisionMask = ~0;
        public float CollisionProbeRadius = 0.2f;

        private float _yaw;
        private float _pitch;

        /// <summary>外部（鼠标/手柄）驱动视角。</summary>
        public void AddLookInput(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw;
            _pitch = Mathf.Clamp(_pitch + deltaPitch, PitchClampDegrees.x, PitchClampDegrees.y);
        }

        protected override CameraView Compute(Transform target, float deltaTime)
        {
            Vector3 pivot = (target != null ? target.position : Vector3.zero) + PivotOffset;
            Quaternion orientation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 boom = orientation * Vector3.back; // 指向相机（角色背后）
            float reach = ArmLength;

            // 碰撞拉近：从轴心沿吊臂球扫，遇阻缩短
            if (Physics.SphereCast(pivot, CollisionProbeRadius, boom, out var hit, ArmLength, CollisionMask, QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(0.1f, hit.distance);

            Vector3 eye = pivot + boom * reach;
            return new CameraView
            {
                Position = eye,
                Rotation = Quaternion.LookRotation((pivot - eye).normalized, Vector3.up),
                FieldOfView = FieldOfView
            };
        }
    }
}
