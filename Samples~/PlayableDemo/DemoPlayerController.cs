// Demo 玩家控制器：用新输入系统读键鼠，驱动一个极简 CharacterController 移动 + 近战技能 + 第三人称相机。
// 注：本 demo 刻意用最简单的 CharacterController 直推移动，不依赖 movement 配套包（com.likeon.gas.movement）；
// 完整的运动状态机 / 运动动画层在该配套包里单独演示。
using System.Collections;
using Likeon.GAS;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GASDemo
{
    public class DemoPlayerController : MonoBehaviour
    {
        [HideInInspector] public AbilitySystemComponent ASC;
        [HideInInspector] public CharacterController Controller;
        [HideInInspector] public MeleeAttackTrace Melee;
        [HideInInspector] public ThirdPersonCameraBehavior ThirdPersonCamera;

        public GameplayTag MeleeAbilityTag = GameplayTag.RequestTag("Ability.MeleeAttack");
        public float MouseSensitivity = 0.12f;
        public float WalkSpeed = 5f;
        public float SprintSpeed = 8f;
        public float RotationSpeed = 12f;
        public float Gravity = -20f;
        public float AttackWindow = 0.3f;
        public float AttackCooldown = 0.5f;
        public float StaminaRegenPerSecond = 15f;

        private float _nextAttackTime;
        private float _verticalVelocity;
        private AS_Stamina _stamina;

        private void Start()
        {
            if (ASC != null) _stamina = ASC.GetAttributeSet<AS_Stamina>();
        }

        private void Update()
        {
            HandleLook();
            HandleMove();
            HandleAttack();
            RegenStamina();
        }

        private void HandleLook()
        {
            if (ThirdPersonCamera == null || Mouse.current == null) return;
            Vector2 d = Mouse.current.delta.ReadValue();
            ThirdPersonCamera.AddLookInput(d.x * MouseSensitivity, -d.y * MouseSensitivity);
        }

        private void HandleMove()
        {
            if (Controller == null) return;

            Vector2 axis = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) axis.y += 1;
                if (kb.sKey.isPressed) axis.y -= 1;
                if (kb.dKey.isPressed) axis.x += 1;
                if (kb.aKey.isPressed) axis.x -= 1;
            }

            // 相机相对方向（在地面平面上）
            Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right; right.y = 0; right.Normalize();
            Vector3 dir = fwd * axis.y + right * axis.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            bool sprint = kb != null && kb.leftShiftKey.isPressed && axis.sqrMagnitude > 0.01f;
            float speed = sprint ? SprintSpeed : WalkSpeed;

            // 重力（贴地时保持轻微下压，避免悬空）
            if (Controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += Gravity * Time.deltaTime;

            Vector3 velocity = dir * speed + Vector3.up * _verticalVelocity;
            Controller.Move(velocity * Time.deltaTime);

            // 朝移动方向转身
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, RotationSpeed * Time.deltaTime);
            }
        }

        private void HandleAttack()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (pressed) TryAttack();
        }

        /// <summary>触发一次攻击：激活近战技能（开判定），按时长关闭判定窗口。供控制器与测试调用。</summary>
        public void TryAttack()
        {
            if (Time.time < _nextAttackTime || ASC == null) return;
            _nextAttackTime = Time.time + AttackCooldown;
            if (ASC.TryActivateAbilitiesByTag(MeleeAbilityTag))
                StartCoroutine(CloseAttackWindow());
        }

        private IEnumerator CloseAttackWindow()
        {
            yield return new WaitForSeconds(AttackWindow);
            if (Melee != null) Melee.EndAttackTrace();
        }

        private void RegenStamina()
        {
            if (_stamina == null || ASC == null) return;
            if (_stamina.Stamina.CurrentValue < _stamina.MaxStamina.CurrentValue)
                ASC.ApplyModToAttributeBase(_stamina.StaminaAttribute, EAttributeModifierOp.Add, StaminaRegenPerSecond * Time.deltaTime);
        }
    }
}
