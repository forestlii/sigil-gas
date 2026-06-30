// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 武器对象的统一接口。
// 单机取舍：保留武器的核心契约。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>武器接口：拥有者、武器标签、激活态、枪口（远程起点）、背靠数据来源、瞄准开关。</summary>
    public interface IWeapon
    {
        /// <summary>持有此武器的角色。</summary>
        GameObject WeaponOwner { get; }

        /// <summary>武器标签（武器类型/词缀，供技能 TagRelationship 与输入多态门控）。</summary>
        GameplayTagContainer WeaponTags { get; }

        /// <summary>武器是否激活（出鞘 / 判定开启）。</summary>
        bool IsWeaponActive { get; }

        /// <summary>设置激活态。</summary>
        void SetWeaponActive(bool active);

        /// <summary>瞄准/发射起点变换（远程武器枪口）。</summary>
        Transform MuzzleTransform { get; }

        /// <summary>武器背靠的来源对象（装备实例 / 数据资产；对齐 UE 武器接口的 SourceObject）。
        /// 让武器关联到具体装备或数据行——做装备/掉落/数据表系统时取它溯源。可空。</summary>
        Object SourceObject { get; set; }

        /// <summary>武器是否处于瞄准态（区别于锁定系统：这是武器层的开火/瞄准开关）。</summary>
        bool IsTargeting { get; }

        /// <summary>设置武器瞄准态（对齐 UE 武器接口的 ToggleTargeting；状态变化广播 <see cref="WeaponComponent.OnTargetingChanged"/>）。</summary>
        void SetTargeting(bool targeting);
    }
}
