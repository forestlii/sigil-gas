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
    [AddComponentMenu("Sigil/GAS/Weapon Component")]
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
        [Tooltip("额外判定实例（多 trace 段）：与主判定同开同关，用于双刃/主副多段碰撞。对齐 UE 武器的多 TraceInstance")]
        [SerializeField] private List<WeaponTraceInstance> additionalTraces = new List<WeaponTraceInstance>();

        [Header("来源对象")]
        [Tooltip("武器背靠的装备实例 / 数据资产（对齐 UE 武器接口 SourceObject）；做装备/掉落/数据表系统时取它溯源。可空")]
        [SerializeField] private UnityEngine.Object sourceObject;

        /// <summary>一段判定实例（一个近战判定 + 选用的配置下标），用于一把武器同时驱动多套碰撞判定。</summary>
        [Serializable]
        public struct WeaponTraceInstance
        {
            public MeleeAttackTrace Trace;
            public int EntryIndex;
        }

        public event Action<bool> OnWeaponActiveStateChanged;
        public event Action<GameObject> OnEquipped;
        public event Action OnUnequipped;
        /// <summary>武器瞄准态变化（武器层开火/瞄准开关，区别于锁定系统）。</summary>
        public event Action<bool> OnTargetingChanged;

        private GameObject _owner;
        private AbilitySystemComponent _ownerASC;
        private bool _active;
        private bool _targeting;

        // ---- IWeapon ----
        public GameObject WeaponOwner => _owner;
        public GameplayTagContainer WeaponTags => weaponTags;
        public bool IsWeaponActive => _active;
        public Transform MuzzleTransform => muzzle != null ? muzzle : transform;

        public AbilitySystemComponent OwnerASC => _ownerASC;
        public MeleeAttackTrace MeleeTrace { get => meleeTrace; set => meleeTrace = value; }

        public UnityEngine.Object SourceObject { get => sourceObject; set => sourceObject = value; }
        public bool IsTargeting => _targeting;

        /// <summary>额外判定实例列表（多 trace 段），运行时也可增删。</summary>
        public List<WeaponTraceInstance> AdditionalTraces => additionalTraces;

        /// <summary>设置武器瞄准态（武器层开火/瞄准开关，区别于锁定系统）。状态变化广播 <see cref="OnTargetingChanged"/>。</summary>
        public void SetTargeting(bool targeting)
        {
            if (targeting == _targeting) return;
            _targeting = targeting;
            OnTargetingChanged?.Invoke(targeting);
        }

        /// <summary>翻转武器瞄准态。</summary>
        public void ToggleTargeting() => SetTargeting(!_targeting);

        /// <summary>把所有判定实例（主 + 额外）的攻击来源重设为当前 owner ASC（装备/换主后调用；对齐 UE RefreshTraceInstance）。</summary>
        public void RefreshTraceInstances()
        {
            if (meleeTrace != null) meleeTrace.SetSource(_ownerASC);
            for (int i = 0; i < additionalTraces.Count; i++)
                if (additionalTraces[i].Trace != null) additionalTraces[i].Trace.SetSource(_ownerASC);
        }

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

            RefreshTraceInstances(); // 主 + 额外判定实例都设来源

            if (applyWeaponTagsToOwner && _ownerASC != null)
                foreach (var t in weaponTags) _ownerASC.AddLooseGameplayTag(t);

            OnEquipped?.Invoke(owner);
        }

        /// <summary>卸下：停用、移除注入的武器标签、断开 owner。</summary>
        public void Unequip()
        {
            SetWeaponActive(false);
            SetTargeting(false);

            if (applyWeaponTagsToOwner && _ownerASC != null)
                foreach (var t in weaponTags) _ownerASC.RemoveLooseGameplayTag(t);

            _owner = null;
            _ownerASC = null;
            OnUnequipped?.Invoke();
        }

        /// <summary>切换激活态：驱动所有判定实例（主 + 额外多 trace 段）+ 触发事件。</summary>
        public void SetWeaponActive(bool active)
        {
            if (active == _active) return;
            _active = active;

            DriveTrace(meleeTrace, meleeTraceIndex, active);
            for (int i = 0; i < additionalTraces.Count; i++)
                DriveTrace(additionalTraces[i].Trace, additionalTraces[i].EntryIndex, active);

            OnWeaponActiveStateChanged?.Invoke(active);
        }

        private static void DriveTrace(MeleeAttackTrace trace, int index, bool active)
        {
            if (trace == null) return;
            if (active) trace.BeginAttackTrace(index);
            else trace.EndAttackTrace();
        }

        /// <summary>远程便捷：从枪口按子弹定义发射（朝枪口前方）。复用 BulletLauncher。</summary>
        public List<BulletInstance> FireProjectile(BulletDefinition def)
        {
            var m = MuzzleTransform;
            return BulletLauncher.Fire(def, _owner, _ownerASC, m.position, m.forward);
        }
    }
}
