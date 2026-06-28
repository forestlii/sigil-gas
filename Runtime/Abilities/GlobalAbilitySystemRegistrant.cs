// Copyright 2026 Likeon All Rights Reserved.
// 可选便利组件：挂在带 AbilitySystemComponent 的角色上，启用时自动注册到 GlobalAbilitySystem、禁用时注销。
// 不想自动注册的工程可不挂此组件，改为手动调 GlobalAbilitySystem.Instance.RegisterASC/UnregisterASC。

using UnityEngine;

namespace Likeon.GAS
{
    /// <summary>
    /// 把宿主的 <see cref="AbilitySystemComponent"/> 在 OnEnable 注册进 <see cref="GlobalAbilitySystem"/>、OnDisable 注销。
    /// 故意做成可选组件而非改 ASC 核心——避免在 ASC 半初始化态被授全局技能/效果。
    /// </summary>
    [AddComponentMenu("Likeon/GAS/Global Ability System Registrant")]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public sealed class GlobalAbilitySystemRegistrant : MonoBehaviour
    {
        private AbilitySystemComponent _asc;

        private void OnEnable()
        {
            if (_asc == null) _asc = GetComponent<AbilitySystemComponent>();
            GlobalAbilitySystem.Instance.RegisterASC(_asc);
        }

        private void OnDisable()
        {
            if (_asc != null) GlobalAbilitySystem.Instance.UnregisterASC(_asc);
        }
    }
}
