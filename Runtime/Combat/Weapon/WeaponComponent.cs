// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 武器的默认实现（持标签/激活态/挥砍判定/枪口）。
// 单机取舍：这里用 MonoBehaviour。
// 武器作枢纽：装备到角色（关联 owner ASC、可把武器标签挂到 owner 供技能门控）、
// 激活态切换（可选驱动挂在武器上的 MeleeAttackTrace 挥砍判定）、远程从枪口发射子弹。
// 注：故不采用，挂点用普通 Transform 父子。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.GAS
{
    [AddComponentMenu("Likeon/GAS/Weapon Component")]
    public class WeaponComponent : MonoBehaviour, IWeapon
    {
        [Header("武器标签")]
        [Tooltip("武器类型/词缀标签（供技能 TagRelationship、输入多态门控）")]
        [SerializeField] private GameplayTagContainer weaponTags = new GameplayTagContainer();
        [Tooltip("装备时把武器标签作为松散标签挂到持有者 ASC（让技能可按'当前武器'门控）")]
        [SerializeField] private bool applyWeaponTagsToOwner = true;

        [Header("挂点 / 枪口")]
        [Tooltip("远程武器的发射起点（留空用自身 transform）")]
        [SerializeField] private Transform muzzle;

        [Header("挥砍判定（可选）")]
        [Tooltip("挂在武器上的近战判定；激活时 Begin、停用时 End")]
        [SerializeField] private MeleeAttackTrace meleeTrace;
        [Tooltip("激活时用哪个判定配置下标")]
        [SerializeField] private int meleeTraceIndex = 0;

        public event Action<bool> OnWeaponActiveStateChanged;
        public event Action<GameObject> OnEquipped;
        public event Action OnUnequipped;

        private GameObject _owner;
        private AbilitySystemComponent _ownerASC;
        private bool _active;

        // ---- IWeapon ----
        public GameObject WeaponOwner => _owner;
        public GameplayTagContainer WeaponTags => weaponTags;
        public bool IsWeaponActive => _active;
        public Transform MuzzleTransform => muzzle != null ? muzzle : transform;

        public AbilitySystemComponent OwnerASC => _ownerASC;
        public MeleeAttackTrace MeleeTrace { get => meleeTrace; set => meleeTrace = value; }

        /// <summary>装备到角色：关联 owner、可挂到挂点、把武器标签注入 owner ASC、给挥砍判定设来源。</summary>
        public void Equip(GameObject owner, Transform attachSocket = null)
        {
            _owner = owner;
            _ownerASC = owner != null ? owner.GetComponentInParent<AbilitySystemComponent>() : null;

            if (attachSocket != null)
            {
                transform.SetParent(attachSocket, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            if (meleeTrace != null) meleeTrace.SetSource(_ownerASC);

            if (applyWeaponTagsToOwner && _ownerASC != null)
                foreach (var t in weaponTags) _ownerASC.AddLooseGameplayTag(t);

            OnEquipped?.Invoke(owner);
        }

        /// <summary>卸下：停用、移除注入的武器标签、断开 owner。</summary>
        public void Unequip()
        {
            SetWeaponActive(false);

            if (applyWeaponTagsToOwner && _ownerASC != null)
                foreach (var t in weaponTags) _ownerASC.RemoveLooseGameplayTag(t);

            _owner = null;
            _ownerASC = null;
            OnUnequipped?.Invoke();
        }

        /// <summary>切换激活态：驱动挥砍判定 + 触发事件。</summary>
        public void SetWeaponActive(bool active)
        {
            if (active == _active) return;
            _active = active;

            if (meleeTrace != null)
            {
                if (active) meleeTrace.BeginAttackTrace(meleeTraceIndex);
                else meleeTrace.EndAttackTrace();
            }

            OnWeaponActiveStateChanged?.Invoke(active);
        }

        /// <summary>远程便捷：从枪口按子弹定义发射（朝枪口前方）。复用 BulletLauncher。</summary>
        public List<BulletInstance> FireProjectile(BulletDefinition def)
        {
            var m = MuzzleTransform;
            return BulletLauncher.Fire(def, _owner, _ownerASC, m.position, m.forward);
        }
    }
}
