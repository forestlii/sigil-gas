// Demo 玩家控制器：读键鼠 → 经 InputSystemComponent 分发 → 激活技能；外加锁定 / 自叠 buff / 武器切换 / 专注 / 载具模式。
// 演示四件事：
//  1) 输入分发：近战/远程/专注键 → ReceiveInput(InputTag) → 控制集处理器 → 激活对应技能（不直接 TryActivate）。
//  2) 载具切换：V 键 Push/Pop 载具控制集 —— 同一个近战键在载具模式改成"鸣笛"。
//  3) block/cancel：G 键专注（激活期间挂 State.Focusing），交互规则令近战被挡、开火远程取消专注。
//  4) 武器→不同技能：1/2 键切剑/斧（WeaponComponent 注入武器标签），近战键据此多态成轻击/重击。
// 注：移动用最简单的 CharacterController 直推（不依赖 movement 配套包）；控制器只"读输入→调框架功能"，不含战斗逻辑本身。
using Likeon.GAS;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Likeon.GAS.Sample.PlayableDemo
{
    public class DemoPlayerController : MonoBehaviour
    {
        // prefab 内部引用 / 资产引用 —— 可见，策划在 prefab 上看得到接线（生成器/运行时也照常代码赋值）
        public AbilitySystemComponent ASC;
        public CharacterController Controller;
        public MeleeAttackTrace Melee;
        public TargetingSystemComponent Targeting;
        public GameplayEffect PowerBuff;       // R 键叠加的 stacking buff
        public InputSystemComponent InputSystem; // 输入分发中枢
        public InputControlSetup VehicleSetup;   // V 键压入的载具控制集
        public WeaponComponent Sword;
        public WeaponComponent Axe;
        // 跨边界：运行时相机模式（纯 C# 对象、不可序列化进 prefab），由 PlayableDemo 运行时接
        [HideInInspector] public ThirdPersonCameraBehavior ThirdPersonCamera;

        // 这些 InputTag 与控制集处理器里配的一致（PlayableDemo 注入）
        public GameplayTag MeleeInputTag = GameplayTag.RequestTag("InputTag.Melee");
        public GameplayTag RangedInputTag = GameplayTag.RequestTag("InputTag.Ranged");
        public GameplayTag FocusInputTag = GameplayTag.RequestTag("InputTag.Focus");

        public float MouseSensitivity = 0.12f;
        public float WalkSpeed = 5f;
        public float SprintSpeed = 8f;
        public float RotationSpeed = 12f;
        public float Gravity = -20f;
        public float AttackCooldown = 0.5f;
        public float RangedCooldown = 0.35f;
        public float StaminaRegenPerSecond = 15f;

        public bool InVehicle { get; private set; }
        public string EquippedWeaponLabel => _equipped == Sword ? "Sword 剑（轻击）" : _equipped == Axe ? "Axe 斧（重击）" : "-";

        private float _nextAttackTime;
        private float _nextRangedTime;
        private float _verticalVelocity;
        private AS_Stamina _stamina;
        private WeaponComponent _equipped;

        private void Start()
        {
            if (ASC != null) _stamina = ASC.GetAttributeSet<AS_Stamina>();
        }

        private void Update()
        {
            HandleLook();
            HandleMove();
            HandleAttack();
            HandleRanged();
            HandleLockOn();
            HandleBuff();
            HandleWeaponSwitch();
            HandleFocus();
            HandleVehicle();
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

            // 转身：锁定时朝目标（侧移环绕感），否则朝移动方向
            Vector3 faceDir = dir;
            if (Targeting != null && Targeting.IsLockedOn)
            {
                Vector3 toTarget = Targeting.TargetedActor.transform.position - transform.position;
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f) faceDir = toTarget.normalized;
            }
            if (faceDir.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(faceDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, RotationSpeed * Time.deltaTime);
            }
        }

        private void HandleAttack()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (pressed) TryAttack();
        }

        private void HandleRanged()
        {
            bool pressed = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                        || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
            if (pressed) TryRanged();
        }

        private void HandleLockOn()
        {
            if (Targeting == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) Targeting.ToggleLock();
            if (kb.qKey.wasPressedThisFrame) Targeting.StaticSwitchToNewTarget(false); // 左
            if (kb.eKey.wasPressedThisFrame) Targeting.StaticSwitchToNewTarget(true);  // 右
        }

        private void HandleBuff()
        {
            if (ASC == null || PowerBuff == null) return;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                ASC.ApplyGameplayEffectToSelf(PowerBuff); // 同组叠层 / 刷新时长
        }

        private void HandleWeaponSwitch()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) EquipSword();
            if (kb.digit2Key.wasPressedThisFrame) EquipAxe();
        }

        private void HandleFocus()
        {
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) Focus();
        }

        private void HandleVehicle()
        {
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) ToggleVehicle();
        }

        // ===================== 对外/测试入口 =====================

        /// <summary>近战：经输入分发激活（近战键 → InputTag.Melee → 控制集按当前武器多态成轻击/重击）。</summary>
        public void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + AttackCooldown;
            InputSystem?.ReceiveInput(MeleeInputTag, InputTriggerEvent.Started, InputActionData.Empty);
        }

        /// <summary>远程：经输入分发激活（远程键 → InputTag.Ranged → 远程技能；会取消专注）。</summary>
        public void TryRanged()
        {
            if (Time.time < _nextRangedTime) return;
            _nextRangedTime = Time.time + RangedCooldown;
            InputSystem?.ReceiveInput(RangedInputTag, InputTriggerEvent.Started, InputActionData.Empty);
        }

        /// <summary>专注：经输入分发激活（持续期间挂 State.Focusing，近战被交互规则挡住）。</summary>
        public void Focus()
        {
            InputSystem?.ReceiveInput(FocusInputTag, InputTriggerEvent.Started, InputActionData.Empty);
        }

        /// <summary>装备剑：注入 Weapon.Sword → 近战键多态到轻击。</summary>
        public void EquipSword() => Equip(Sword);

        /// <summary>装备斧：注入 Weapon.Axe → 近战键多态到重击。</summary>
        public void EquipAxe() => Equip(Axe);

        private void Equip(WeaponComponent weapon)
        {
            if (weapon == null || _equipped == weapon) return;
            if (_equipped != null) _equipped.Unequip();  // 移除旧武器标签
            weapon.Equip(gameObject);                     // 注入新武器标签到 owner ASC
            _equipped = weapon;
        }

        /// <summary>切换载具模式：Push/Pop 载具控制集（同一个近战键改成鸣笛）。</summary>
        public void ToggleVehicle()
        {
            if (InputSystem == null || VehicleSetup == null) return;
            if (InVehicle) { InputSystem.PopInputSetup(); InVehicle = false; }
            else { InputSystem.PushInputSetup(VehicleSetup); InVehicle = true; }
        }

        private void RegenStamina()
        {
            if (_stamina == null || ASC == null) return;
            if (_stamina.Stamina.CurrentValue < _stamina.MaxStamina.CurrentValue)
                ASC.ApplyModToAttributeBase(_stamina.StaminaAttribute, EAttributeModifierOp.Add, StaminaRegenPerSecond * Time.deltaTime);
        }
    }
}
