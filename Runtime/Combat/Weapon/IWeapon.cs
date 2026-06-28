// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 武器对象的统一接口。
// 单机取舍：保留武器的核心契约。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>武器接口：拥有者、武器标签、激活态、枪口（远程起点）。</summary>
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
    }
}
