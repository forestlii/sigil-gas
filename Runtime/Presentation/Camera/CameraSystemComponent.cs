// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 相机系统组件：持有相机混合栈，每帧把融合后的视图应用到 Unity Camera。

using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Sigil/GAS/Camera System Component")]
    public class CameraSystemComponent : MonoBehaviour
    {
        [Tooltip("要驱动的 Unity 相机（留空则用 Camera.main）")]
        [SerializeField] private Camera targetCamera;
        [Tooltip("相机跟随的目标（通常是角色）")]
        [SerializeField] private Transform followTarget;

        [Tooltip("默认相机行为（第三人称）")]
        [SerializeReference] private CameraBehavior defaultBehavior = new ThirdPersonCameraBehavior();

        private readonly CameraBlendStack _stack = new CameraBlendStack();
        public CameraBlendStack Stack => _stack;
        public CameraBehavior DefaultBehavior => defaultBehavior;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (followTarget == null) followTarget = transform;
        }

        private void Start()
        {
            // 默认行为在此压栈（晚于 Awake/Configure，便于代码驱动配置后再压）
            if (_stack.Count == 0 && defaultBehavior != null) _stack.Push(defaultBehavior);
        }

        /// <summary>代码驱动配置：指定相机、跟随目标、默认行为。供运行时搭建/测试用。</summary>
        public void Configure(Camera camera, Transform target, CameraBehavior behavior = null)
        {
            if (camera != null) targetCamera = camera;
            if (target != null) followTarget = target;
            if (behavior != null) defaultBehavior = behavior;
        }

        /// <summary>压入一个相机行为（如进入瞄准）。</summary>
        public void PushBehavior(CameraBehavior behavior) => _stack.Push(behavior);

        /// <summary>弹出一个相机行为（退出瞄准回到默认）。</summary>
        public void PopBehavior(CameraBehavior behavior) => _stack.Pop(behavior);

        private void LateUpdate()
        {
            if (targetCamera == null || followTarget == null) return;
            var view = _stack.Evaluate(followTarget, Time.deltaTime);
            targetCamera.transform.SetPositionAndRotation(view.Position, view.Rotation);
            if (view.FieldOfView > 0f) targetCamera.fieldOfView = view.FieldOfView;
        }
    }
}
